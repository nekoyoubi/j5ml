using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace J5ml;

/// <summary>
/// Writes canonical JSON.
///
/// System.Text.Json cannot produce it. Its encoders are configured with
/// <c>UnicodeRange</c>s, which only describe the Basic Multilingual Plane, so
/// every character above U+FFFF falls outside any allowed range and is written
/// as an escaped surrogate pair. `serde_json` and `JSON.stringify` both emit
/// those characters literally, so a document holding an emoji would serialize to
/// different bytes here than in the other implementations and stop round-tripping.
///
/// Escaping is therefore done here: quote, backslash, and the C0 controls, which
/// is exactly what JSON requires and nothing more.
/// </summary>
internal static class CanonicalJson
{
	internal static string Write(JsonNode? node)
	{
		var builder = new StringBuilder();
		WriteValue(builder, node);
		return builder.ToString();
	}

	private static void WriteValue(StringBuilder builder, JsonNode? node)
	{
		switch (node)
		{
			case null:
				builder.Append("null");
				return;

			case JsonArray array:
				builder.Append('[');
				for (var index = 0; index < array.Count; index++)
				{
					if (index > 0) builder.Append(',');
					WriteValue(builder, array[index]);
				}
				builder.Append(']');
				return;

			case JsonObject map:
				builder.Append('{');
				var first = true;
				foreach (var pair in map)
				{
					if (!first) builder.Append(',');
					first = false;
					WriteString(builder, pair.Key);
					builder.Append(':');
					WriteValue(builder, pair.Value);
				}
				builder.Append('}');
				return;

			case JsonValue value:
				WriteScalar(builder, value);
				return;

			default:
				throw new J5mlException($"cannot serialize a {node.GetType().Name}");
		}
	}

	private static void WriteScalar(StringBuilder builder, JsonValue value)
	{
		if (value.TryGetValue<string>(out var text)) { WriteString(builder, text); return; }
		if (value.TryGetValue<bool>(out var flag)) { builder.Append(flag ? "true" : "false"); return; }

		// A value that came from a JSON parse still carries the number exactly as it
		// was written. Reusing that text keeps a round trip byte-exact instead of
		// reformatting `1e3` into `1000` on the way back out.
		if (value.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.Number)
		{
			builder.Append(element.GetRawText());
			return;
		}

		// A `JsonValue` is typed by whatever the caller handed it, and `TryGetValue`
		// does not widen: a value built from an `int` refuses to come back out as a
		// `long`. Each integer type is therefore asked for by name.
		if (value.TryGetValue<int>(out var i)) { builder.Append(i.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<long>(out var l)) { builder.Append(l.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<short>(out var sh)) { builder.Append(sh.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<sbyte>(out var sb)) { builder.Append(sb.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<uint>(out var ui)) { builder.Append(ui.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<ulong>(out var ul)) { builder.Append(ul.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<ushort>(out var us)) { builder.Append(us.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<byte>(out var b)) { builder.Append(b.ToString(CultureInfo.InvariantCulture)); return; }
		if (value.TryGetValue<decimal>(out var m)) { builder.Append(m.ToString(CultureInfo.InvariantCulture)); return; }

		if (value.TryGetValue<float>(out var f)) { WriteDouble(builder, f); return; }
		if (value.TryGetValue<double>(out var d)) { WriteDouble(builder, d); return; }

		throw new J5mlException("cannot serialize an attribute value of an unknown type");
	}

	private static void WriteDouble(StringBuilder builder, double number)
	{
		if (double.IsNaN(number) || double.IsInfinity(number))
			throw new J5mlException("JSON has no representation for NaN or Infinity");

		// "R" is the shortest representation that reads back identically, which is
		// the same contract `JSON.stringify` and `serde_json` write under.
		builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
	}

	private static void WriteString(StringBuilder builder, string text)
	{
		builder.Append('"');

		foreach (var c in text)
		{
			switch (c)
			{
				case '"': builder.Append("\\\""); break;
				case '\\': builder.Append("\\\\"); break;
				case '\b': builder.Append("\\b"); break;
				case '\f': builder.Append("\\f"); break;
				case '\n': builder.Append("\\n"); break;
				case '\r': builder.Append("\\r"); break;
				case '\t': builder.Append("\\t"); break;
				default:
					if (c < 0x20)
					{
						builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						// Everything else, surrogate pairs included, is written as it
						// stands. The output is UTF-8 and needs no escape to be valid.
						builder.Append(c);
					}
					break;
			}
		}

		builder.Append('"');
	}
}
