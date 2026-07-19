# J5ml

J5ML is a JSON representation of a markup tree.

[![NuGet](https://img.shields.io/nuget/v/J5ml)](https://www.nuget.org/packages/J5ml)

```csharp
using J5ml;

var node = J5mlDocument.Parse("""["div",{"class":"panel"},"Hello"]""");
// Element { Name = "div", Attrs = { ["class"] = "panel" }, Children = [ Text("Hello") ] }

J5mlDocument.Stringify(node); // ["div",{"class":"panel"},"Hello"]
```

Markup describes trees. JSON describes data. J5ML is for documents that have to do both at once.

It is [JsonML](http://www.jsonml.org/syntax/), widened for trees that are not XML. Every valid JsonML document is a valid J5ML document.

What it adds: an attribute value may be any JSON value, including an array or an object, where JsonML's grammar allows only `string`, `number`, `true`, `false`, and `null`. And `Parse` accepts JSON5, so a hand-authored document may carry comments, trailing commas, and unquoted keys:

```csharp
var node = J5mlDocument.Parse("""
  // authored by hand
  ['Gauge', { percent: 62.5, }]
  """);

J5mlDocument.Stringify(node); // ["Gauge",{"percent":62.5}]
```

Serialization is always canonical JSON, never JSON5, so anything this package writes is readable by a plain JSON parser. Use `ParseJson` when JSON5 syntax should be an error.

```json
["Layout", { "constraints": ["Length:1", "Min:0"] }]
["Gauge",  { "style": { "fg": "Green" }, "ratio_bind": "health", "percent": 62.5 }]
```

Names are preserved verbatim, so `Gauge`, `ratio_bind`, `data-bind`, and `svg:circle` all survive a round trip unchanged. Nothing is folded to lowercase on the way through, which is a thing HTML-shaped libraries do and this one does not.

## The tree

A node is an element or a text node, and the shape is the same one the shared conformance corpus uses:

```csharp
public sealed class Element : Node
{
    public string Name { get; }
    public IDictionary<string, JsonNode?> Attrs { get; }
    public IList<Node> Children { get; }
}

public sealed class Text : Node
{
    public string Value { get; }
}
```

An attribute value is a `System.Text.Json.Nodes.JsonNode`, so it holds any JSON value and keeps its type: a number arrives as a number, a list as a list.

Attribute names are ordered by Unicode code point on the way out, recursively through attribute values, so the same tree serializes identically here and in every other implementation. That ordering is by code point rather than by `StringComparer.Ordinal`, which compares UTF-16 code units and disagrees with UTF-8 byte order above U+FFFF.

## Walking a tree

`Traverse` visits a node and its descendants, parents before children, handing the visitor the path to each one:

```csharp
using J5ml;

var tree = J5mlDocument.Parse("""["doc",{},["script",{},"x"]]""");

string? found = null;
J5mlDocument.Traverse(tree, (node, path) =>
{
    if (node is Element { Name: "script" })
    {
        found = J5mlDocument.PathToString(path); // "/0"
        return Walk.Stop;
    }
    return Walk.Continue;
});
```

Return `Walk.Skip` to leave a node's children unvisited and carry on with its siblings, or `Walk.Stop` to end the traversal. An overload taking an `Action` walks the whole tree when there is nothing to decide.

Attributes are not visited and are not part of a path. An attribute value is JSON rather than markup, so walking into one is a different traversal with different rules. A consumer checking attributes reads `Attrs` itself and appends the attribute name to the element's path when reporting, because only that consumer knows what it is rejecting.

## Not a policy

J5ML does not define which element names are legal, what any of them mean, or whether a document is safe to render. `Parse` accepts any well-formed tree, `<script>` included.

A consumer rendering into a medium where markup executes sanitizes it first, because only the consumer knows its own vocabulary and output medium. `Traverse` is the primitive that check is built on.

For the same reason, serialization escapes only what JSON requires and does not escape `<`, `>`, or `&` for HTML embedding. That is the consumer's call to make at its own edge.

## Conformance

This package is asserted against `spec/conformance-tests.json`, the corpus shared by every J5ML implementation in the repository, rather than against a test suite of its own. See `spec/j5ml.md` for the format; where the prose and the corpus disagree, the corpus wins.

## License

MIT OR Apache-2.0.
