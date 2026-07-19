using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace J5ml;

/// <summary>
/// A JSON5 reader that builds a <see cref="JsonNode"/> directly.
///
/// This parses JSON5 rather than rewriting it into JSON and handing that to a
/// JSON parser. A rewrite would have to reproduce string escaping and number
/// formatting exactly to avoid changing the document on the way through, and a
/// position in a syntax error would point at the rewritten text rather than at
/// what the caller wrote.
/// </summary>
internal sealed class Json5Parser
{
	private readonly string _source;
	private int _index;

	private Json5Parser(string source) => _source = source;

	internal static JsonNode? Parse(string source)
	{
		var parser = new Json5Parser(source);
		parser.SkipTrivia();
		var value = parser.ReadValue();
		parser.SkipTrivia();
		if (!parser.AtEnd) throw parser.Error("unexpected trailing content");
		return value;
	}

	private bool AtEnd => _index >= _source.Length;
	private char Current => _source[_index];

	private FormatException Error(string message) =>
		new($"{message} at position {_index}");

	private void SkipTrivia()
	{
		while (!AtEnd)
		{
			var c = Current;
			if (c == '/')
			{
				if (_index + 1 >= _source.Length) return;
				var next = _source[_index + 1];
				if (next == '/')
				{
					_index += 2;
					while (!AtEnd && !IsLineTerminator(Current)) _index++;
					continue;
				}
				if (next == '*')
				{
					_index += 2;
					while (true)
					{
						if (_index + 1 >= _source.Length) throw Error("unterminated block comment");
						if (Current == '*' && _source[_index + 1] == '/') { _index += 2; break; }
						_index++;
					}
					continue;
				}
				return;
			}

			if (char.IsWhiteSpace(c) || c == '\ufeff') { _index++; continue; }
			return;
		}
	}

	private static bool IsLineTerminator(char c) =>
		c is '\n' or '\r' or '\u2028' or '\u2029';

	private JsonNode? ReadValue()
	{
		if (AtEnd) throw Error("expected a value");

		return Current switch
		{
			'{' => ReadObject(),
			'[' => ReadArray(),
			'"' or '\'' => JsonValue.Create(ReadString(Current)),
			_ => ReadKeywordOrNumber(),
		};
	}

	private JsonNode? ReadKeywordOrNumber()
	{
		if (TryConsumeWord("null")) return null;
		if (TryConsumeWord("true")) return JsonValue.Create(true);
		if (TryConsumeWord("false")) return JsonValue.Create(false);
		return ReadNumber();
	}

	private bool TryConsumeWord(string word)
	{
		if (_index + word.Length > _source.Length) return false;
		if (string.CompareOrdinal(_source, _index, word, 0, word.Length) != 0) return false;

		var after = _index + word.Length;
		if (after < _source.Length && IsIdentifierPart(_source[after])) return false;

		_index = after;
		return true;
	}

	private JsonNode ReadObject()
	{
		_index++;
		var result = new JsonObject();

		SkipTrivia();
		if (!AtEnd && Current == '}') { _index++; return result; }

		while (true)
		{
			SkipTrivia();
			if (AtEnd) throw Error("unterminated object");

			// A trailing comma leaves the closing brace here.
			if (Current == '}') { _index++; return result; }

			var key = Current is '"' or '\'' ? ReadString(Current) : ReadIdentifier();

			SkipTrivia();
			if (AtEnd || Current != ':') throw Error("expected ':' after an object key");
			_index++;

			SkipTrivia();
			result[key] = ReadValue();

			SkipTrivia();
			if (AtEnd) throw Error("unterminated object");
			if (Current == ',') { _index++; continue; }
			if (Current == '}') { _index++; return result; }
			throw Error("expected ',' or '}' in an object");
		}
	}

	private JsonNode ReadArray()
	{
		_index++;
		var result = new JsonArray();

		SkipTrivia();
		if (!AtEnd && Current == ']') { _index++; return result; }

		while (true)
		{
			SkipTrivia();
			if (AtEnd) throw Error("unterminated array");

			if (Current == ']') { _index++; return result; }

			result.Add(ReadValue());

			SkipTrivia();
			if (AtEnd) throw Error("unterminated array");
			if (Current == ',') { _index++; continue; }
			if (Current == ']') { _index++; return result; }
			throw Error("expected ',' or ']' in an array");
		}
	}

	private string ReadString(char quote)
	{
		_index++;
		var builder = new StringBuilder();

		while (true)
		{
			if (AtEnd) throw Error("unterminated string");
			var c = Current;

			if (c == quote) { _index++; return builder.ToString(); }

			if (c == '\\') { _index++; ReadEscape(builder); continue; }

			if (IsLineTerminator(c)) throw Error("a line terminator must be escaped inside a string");

			builder.Append(c);
			_index++;
		}
	}

	private void ReadEscape(StringBuilder builder)
	{
		if (AtEnd) throw Error("unterminated escape");
		var c = Current;
		_index++;

		switch (c)
		{
			case 'n': builder.Append('\n'); return;
			case 'r': builder.Append('\r'); return;
			case 't': builder.Append('\t'); return;
			case 'b': builder.Append('\b'); return;
			case 'f': builder.Append('\f'); return;
			case 'v': builder.Append('\v'); return;
			case '0':
				// `\0` is a NUL only when a digit does not follow; `\01` is a legacy
				// octal escape, which JSON5 does not carry over from JavaScript.
				if (!AtEnd && char.IsDigit(Current)) throw Error("octal escapes are not valid");
				builder.Append('\0');
				return;
			case 'x': builder.Append(ReadHex(2)); return;
			case 'u': builder.Append(ReadHex(4)); return;
			case '\r':
				// A CRLF line continuation consumes both halves.
				if (!AtEnd && Current == '\n') _index++;
				return;
			case '\n':
			case '\u2028':
			case '\u2029':
				return;
			default:
				if (char.IsDigit(c)) throw Error("octal escapes are not valid");
				builder.Append(c);
				return;
		}
	}

	private char ReadHex(int digits)
	{
		if (_index + digits > _source.Length) throw Error("truncated escape");
		var text = _source.Substring(_index, digits);
		if (!ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
			throw Error("an escape must be hexadecimal");
		_index += digits;
		return (char)value;
	}

	private string ReadIdentifier()
	{
		var start = _index;
		if (AtEnd || !IsIdentifierStart(Current)) throw Error("expected an object key");

		_index++;
		while (!AtEnd && IsIdentifierPart(Current)) _index++;

		return _source.Substring(start, _index - start);
	}

	private static bool IsIdentifierStart(char c) =>
		char.IsLetter(c) || c == '$' || c == '_';

	private static bool IsIdentifierPart(char c) =>
		char.IsLetterOrDigit(c) || c == '$' || c == '_';

	private JsonNode ReadNumber()
	{
		var start = _index;
		var negative = false;

		if (!AtEnd && (Current == '+' || Current == '-'))
		{
			negative = Current == '-';
			_index++;
		}

		if (TryConsumeWord("Infinity"))
			return JsonValue.Create(negative ? double.NegativeInfinity : double.PositiveInfinity);

		if (TryConsumeWord("NaN")) return JsonValue.Create(double.NaN);

		if (!AtEnd && Current == '0' && _index + 1 < _source.Length &&
			(_source[_index + 1] == 'x' || _source[_index + 1] == 'X'))
		{
			_index += 2;
			var hexStart = _index;
			while (!AtEnd && Uri.IsHexDigit(Current)) _index++;
			if (_index == hexStart) throw Error("a hexadecimal literal needs at least one digit");

			var digits = _source.Substring(hexStart, _index - hexStart);
			if (!long.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
				throw Error("hexadecimal literal out of range");
			return JsonValue.Create(negative ? -hex : hex);
		}

		var integral = true;

		while (!AtEnd && char.IsAsciiDigit(Current)) _index++;

		if (!AtEnd && Current == '.')
		{
			integral = false;
			_index++;
			while (!AtEnd && char.IsAsciiDigit(Current)) _index++;
		}

		if (!AtEnd && (Current == 'e' || Current == 'E'))
		{
			integral = false;
			_index++;
			if (!AtEnd && (Current == '+' || Current == '-')) _index++;
			var expStart = _index;
			while (!AtEnd && char.IsAsciiDigit(Current)) _index++;
			if (_index == expStart) throw Error("an exponent needs at least one digit");
		}

		var text = _source.Substring(start, _index - start);
		if (text.Length == 0 || text == "+" || text == "-") throw Error("expected a value");

		// An integer stays an integer so serialization does not grow a fractional
		// part the source never had. The corpus pins this: `100`, never `100.0`.
		if (integral && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
			return JsonValue.Create(whole);

		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
			throw Error("malformed number");

		return JsonValue.Create(number);
	}
}
