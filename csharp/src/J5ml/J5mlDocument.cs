using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace J5ml;

/// <summary>
/// How a traversal continues past the node just visited.
/// </summary>
public enum Walk
{
	/// <summary>Descend into the node's children.</summary>
	Continue,
	/// <summary>Leave the node's children unvisited and carry on with its siblings.</summary>
	Skip,
	/// <summary>End the traversal.</summary>
	Stop,
}

/// <summary>
/// Reads and writes J5ML documents.
/// </summary>
public static class J5mlDocument
{
	private const string NameRequired = "an element's first member must be a non-empty name";
	private const string DocumentShape = "a document is an element or a text node";
	private const string ChildShape = "a child member must be a string or an array";


	/// <summary>
	/// Parses a J5ML document.
	///
	/// Accepts JSON5, which subsumes JSON: comments, trailing commas, unquoted keys,
	/// and single-quoted strings all parse, and a plain JSON document parses
	/// identically. There is no mode to select.
	/// </summary>
	public static Node Parse(string source)
	{
		JsonNode? value;
		try
		{
			value = Json5Parser.Parse(source);
		}
		catch (Exception cause) when (cause is not J5mlException)
		{
			throw new J5mlException($"J5ML parse failed: {cause.Message}", cause);
		}

		return ToNode(value, isDocument: true);
	}

	/// <summary>
	/// Parses a J5ML document, rejecting anything JSON would reject.
	///
	/// Use when a document is expected to already be canonical and JSON5 authoring
	/// syntax should be an error rather than an accepted input.
	/// </summary>
	public static Node ParseJson(string source)
	{
		JsonNode? value;
		try
		{
			// The defaults reject comments and trailing commas, which is the whole
			// point of this entry point.
			value = JsonNode.Parse(source);
		}
		catch (Exception cause)
		{
			throw new J5mlException($"J5ML parse failed: {cause.Message}", cause);
		}

		return ToNode(value, isDocument: true);
	}

	/// <summary>
	/// Serializes a J5ML document to canonical JSON.
	///
	/// Never emits JSON5 syntax. Comments and trailing commas in a parsed source do
	/// not survive, because they are authoring affordances rather than document
	/// content.
	/// </summary>
	public static string Stringify(Node node) =>
		CanonicalJson.Write(ToJson(node));

	/// <summary>
	/// Visits a node and its descendants in document order, parents before children.
	///
	/// The traversal primitive a consumer builds its own validation or sanitization
	/// on: J5ML does not define which names are legal or whether a tree is safe to
	/// render, so those checks belong to the consumer that knows its output medium.
	/// The path is what lets a rejection say <em>where</em>.
	///
	/// <para>
	/// Attributes are not visited and are not part of a path. An attribute value is
	/// JSON rather than markup, so walking into one is a different traversal with
	/// different rules. A consumer checking attributes reads <see cref="Element.Attrs"/>
	/// itself and appends the attribute name to the element's path when reporting,
	/// because only that consumer knows what it is rejecting.
	/// </para>
	/// </summary>
	public static void Traverse(Node node, Func<Node, IReadOnlyList<int>, Walk> visit) =>
		WalkFrom(node, new List<int>(), visit);

	/// <summary>
	/// Visits a node and its descendants without deciding whether to continue.
	///
	/// The whole tree is walked. Use the <see cref="Walk"/>-returning overload to
	/// skip a subtree or stop early.
	/// </summary>
	public static void Traverse(Node node, Action<Node, IReadOnlyList<int>> visit) =>
		WalkFrom(node, new List<int>(), (visited, path) =>
		{
			visit(visited, path);
			return Walk.Continue;
		});

	private static Walk WalkFrom(Node node, List<int> trail, Func<Node, IReadOnlyList<int>, Walk> visit)
	{
		// A copy, not the live trail: a consumer that keeps a path would otherwise
		// hold a list this traversal keeps mutating underneath it.
		var outcome = visit(node, trail.ToArray());
		if (outcome == Walk.Stop) return Walk.Stop;
		if (outcome == Walk.Skip) return Walk.Continue;

		if (node is Element element)
		{
			for (var index = 0; index < element.Children.Count; index++)
			{
				trail.Add(index);
				var result = WalkFrom(element.Children[index], trail, visit);
				trail.RemoveAt(trail.Count - 1);
				if (result == Walk.Stop) return Walk.Stop;
			}
		}

		return Walk.Continue;
	}

	/// <summary>Renders a path as <c>/0/2</c>, and as the empty string at the root.</summary>
	public static string PathToString(IReadOnlyList<int> path)
	{
		var builder = new StringBuilder();
		foreach (var index in path) builder.Append('/').Append(index);
		return builder.ToString();
	}

	private static Node ToNode(JsonNode? value, bool isDocument = false)
	{
		if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) return new Text(text);
		if (value is JsonArray members) return ElementFromMembers(members);
		throw new J5mlException(isDocument ? DocumentShape : ChildShape);
	}

	private static Element ElementFromMembers(JsonArray members)
	{
		if (members.Count == 0 ||
			members[0] is not JsonValue first ||
			!first.TryGetValue<string>(out var name) ||
			name.Length == 0)
		{
			throw new J5mlException(NameRequired);
		}

		var attrs = new Dictionary<string, JsonNode?>();
		var children = new List<Node>();

		// The second member is attributes only when it is an object; a child is
		// always a string or an array, so the two can never be confused.
		var index = 1;
		if (members.Count > 1 && members[1] is JsonObject map)
		{
			foreach (var pair in map)
			{
				attrs[pair.Key] = pair.Value?.DeepClone();
			}
			index = 2;
		}

		for (; index < members.Count; index++)
		{
			children.Add(ToNode(members[index]));
		}

		return new Element(name, attrs, children);
	}

	private static JsonNode? ToJson(Node node)
	{
		if (node is Text text) return JsonValue.Create(text.Value);

		var element = (Element)node;
		if (element.Name.Length == 0) throw new J5mlException(NameRequired);

		var members = new JsonArray { JsonValue.Create(element.Name) };

		if (element.Attrs.Count > 0)
		{
			var ordered = new JsonObject();
			foreach (var key in Sorted(element.Attrs.Keys))
			{
				ordered[key] = Canonical(element.Attrs[key]);
			}
			members.Add(ordered);
		}

		foreach (var child in element.Children)
		{
			members.Add(ToJson(child));
		}

		return members;
	}

	/// <summary>
	/// Orders object keys so serialization is deterministic across implementations.
	/// Applies inside attribute values too, not only at the attribute map itself.
	/// </summary>
	private static JsonNode? Canonical(JsonNode? value)
	{
		switch (value)
		{
			case JsonArray array:
			{
				var result = new JsonArray();
				foreach (var item in array) result.Add(Canonical(item));
				return result;
			}
			case JsonObject map:
			{
				var result = new JsonObject();
				var keys = map.Select(pair => pair.Key);
				foreach (var key in Sorted(keys)) result[key] = Canonical(map[key]);
				return result;
			}
			default:
				return value?.DeepClone();
		}
	}

	private static IEnumerable<string> Sorted(IEnumerable<string> keys) =>
		keys.OrderBy(key => key, CodePointComparer.Instance);

	/// <summary>
	/// Orders by Unicode code point rather than by .NET's ordinal comparison, which
	/// compares UTF-16 code units and disagrees with UTF-8 byte order for anything
	/// above U+FFFF. Implementations that sort by bytes would otherwise emit
	/// different documents for the same tree.
	/// </summary>
	private sealed class CodePointComparer : IComparer<string>
	{
		internal static readonly CodePointComparer Instance = new();

		public int Compare(string? a, string? b)
		{
			if (ReferenceEquals(a, b)) return 0;
			if (a is null) return -1;
			if (b is null) return 1;

			var left = a.EnumerateRunes();
			var right = b.EnumerateRunes();

			while (true)
			{
				var hasLeft = left.MoveNext();
				var hasRight = right.MoveNext();

				if (!hasLeft) return hasRight ? -1 : 0;
				if (!hasRight) return 1;

				var difference = left.Current.Value - right.Current.Value;
				if (difference != 0) return difference;
			}
		}
	}
}
