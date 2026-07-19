using System.Text.Json.Nodes;

namespace J5ml;

/// <summary>
/// A node in a J5ML document: either an <see cref="Element"/> or a <see cref="Text"/>.
/// </summary>
public abstract class Node
{
	private protected Node() { }
}

/// <summary>
/// An element: <c>["name", { attributes }, ...children]</c>.
/// </summary>
public sealed class Element : Node
{
	/// <summary>The element name, exactly as authored. Never folded, never validated.</summary>
	public string Name { get; }

	/// <summary>
	/// Attributes. Any JSON value is a legal attribute value, arrays and objects included.
	/// </summary>
	public IDictionary<string, JsonNode?> Attrs { get; }

	/// <summary>Child nodes, in document order.</summary>
	public IList<Node> Children { get; }

	/// <summary>Builds an element.</summary>
	public Element(string name, IDictionary<string, JsonNode?>? attrs = null, IList<Node>? children = null)
	{
		Name = name;
		Attrs = attrs ?? new Dictionary<string, JsonNode?>();
		Children = children ?? new List<Node>();
	}
}

/// <summary>
/// A text node.
/// </summary>
public sealed class Text : Node
{
	/// <summary>The text content.</summary>
	public string Value { get; }

	/// <summary>Builds a text node.</summary>
	public Text(string value) => Value = value;
}
