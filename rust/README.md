# j5ml

J5ML is a JSON representation of a markup tree.

[![Crates.io](https://img.shields.io/crates/v/j5ml)](https://crates.io/crates/j5ml)

```rust
use j5ml::{from_str, to_string, Node};

let node = from_str(r#"["div",{"class":"panel"},"Hello"]"#).unwrap();
let element = node.as_element().unwrap();
assert_eq!(element.name, "div");
assert_eq!(to_string(&node).unwrap(), r#"["div",{"class":"panel"},"Hello"]"#);
```

Markup describes trees. JSON describes data. J5ML is for documents that have to do both at once.

It is [JsonML](http://www.jsonml.org/syntax/), widened for trees that are not XML. Every valid JsonML document is a valid J5ML document.

What it adds: an attribute value may be any JSON value, including an array or an object, where JsonML's grammar allows only `string`, `number`, `true`, `false`, and `null`. And `from_str` accepts JSON5, so a hand-authored document may carry comments, trailing commas, and unquoted keys:

```rust
let node = j5ml::from_str("
    // authored by hand
    ['Gauge', { percent: 62.5, }]
").unwrap();
assert_eq!(j5ml::to_string(&node).unwrap(), r#"["Gauge",{"percent":62.5}]"#);
```

Serialization is always canonical JSON, never JSON5, so anything this crate writes is readable by a plain JSON parser. Use `from_json_str` when JSON5 syntax should be an error.

```json
["Layout", { "constraints": ["Length:1", "Min:0"] }]
["Gauge",  { "style": { "fg": "Green" }, "ratio_bind": "health", "percent": 62.5 }]
```

Names are preserved verbatim, so `Gauge`, `ratio_bind`, `data-bind`, and `svg:circle` all survive a round trip unchanged.

Attribute names are ordered on serialization, including keys nested inside an attribute value, so two implementations emit the same bytes for the same tree. Attributes live in a `BTreeMap`, so this is the natural order rather than a sorting pass.

J5ML is a format, not a policy. It does not define which names are legal, what they mean, or whether a document is safe to render. A consumer that renders J5ML into a medium where markup is executable must sanitize it first.

`Node::walk` is the traversal that check is built on. It visits a node and every descendant, parents before children, handing the visitor each node's `Path` so a rejection can say *where*. Return `Walk::SkipChildren` to leave a subtree alone, or `Walk::Stop` to end at the first finding.

```rust
use j5ml::{from_str, Walk};

let tree = from_str(r#"["doc",{},["script",{},"x"]]"#).unwrap();
let mut found = None;
tree.walk(&mut |node, path| {
    if node.as_element().is_some_and(|e| e.name == "script") {
        found = Some(path.to_string());
        return Walk::Stop;
    }
    Walk::Continue
});
assert_eq!(found.as_deref(), Some("/0"));
```

See `spec/j5ml.md` in the repository for the format, and `spec/conformance-tests.json` for the shared corpus every implementation is asserted against.

If you are round-tripping XML, [`jsonml`](https://crates.io/crates/jsonml) implements the original grammar faithfully and is the better fit.

## License

MIT OR Apache-2.0.
