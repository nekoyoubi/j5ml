# j5ml

J5ML is a JSON representation of a markup tree.

```ts
import { parse, stringify } from "j5ml";

const node = parse('["div",{"class":"panel"},"Hello"]');
// { name: "div", attrs: { class: "panel" }, children: [{ text: "Hello" }] }

stringify(node); // '["div",{"class":"panel"},"Hello"]'
```

Markup describes trees. JSON describes data. J5ML is for documents that have to do both at once.

It is [JsonML](http://www.jsonml.org/syntax/), widened for trees that are not XML. Every valid JsonML document is a valid J5ML document.

What it adds: an attribute value may be any JSON value, including an array or an object, where JsonML's grammar allows only `string`, `number`, `true`, `false`, and `null`. And `parse` accepts JSON5, so a hand-authored document may carry comments, trailing commas, and unquoted keys:

```ts
const node = parse(`
  // authored by hand
  ['Gauge', { percent: 62.5, }]
`);
stringify(node); // '["Gauge",{"percent":62.5}]'
```

Serialization is always canonical JSON, never JSON5, so anything this package writes is readable by a plain JSON parser. Use `parseJson` when JSON5 syntax should be an error.

```json
["Layout", { "constraints": ["Length:1", "Min:0"] }]
["Gauge",  { "style": { "fg": "Green" }, "ratio_bind": "health", "percent": 62.5 }]
```

Names are preserved verbatim, so `Gauge`, `ratio_bind`, `data-bind`, and `svg:circle` all survive a round trip unchanged. Nothing is folded to lowercase on the way through, which is a thing HTML-shaped libraries do and this one does not.

## The tree

A node is an element or a text node, and the shape is the same one the shared conformance corpus uses:

```ts
interface Element {
	name: string;
	attrs: Record<string, JsonValue>;
	children: Node[];
}

interface Text {
	text: string;
}

type Node = Element | Text;
```

`isElement` and `isText` narrow it. `element(name, attrs?, children?)` and `text(value)` build one.

Attribute names are ordered on serialization, including keys nested inside an attribute value, so two implementations emit the same bytes for the same tree. Ordering is by code point rather than by JavaScript's default comparison, which orders by UTF-16 code unit and disagrees with UTF-8 byte order above U+FFFF.

## Not a policy

J5ML is a format, not a policy. It does not define which names are legal, what they mean, or whether a document is safe to render. A consumer that renders J5ML into a medium where markup is executable must sanitize it first.

`walk` is the traversal that check is built on. It visits a node and every descendant, parents before children, handing the visitor each node's path so a rejection can say *where*. Return `"skip"` to leave a subtree alone, or `"stop"` to end at the first finding; returning nothing continues.

```ts
import { isElement, parse, pathToString, walk } from "j5ml";

const tree = parse('["doc",{},["script",{},"x"]]');

let found: string | undefined;
walk(tree, (node, path) => {
	if (isElement(node) && node.name === "script") {
		found = pathToString(path);
		return "stop";
	}
});

console.assert(found === "/0");
```

See `spec/j5ml.md` in the repository for the format, and `spec/conformance-tests.json` for the shared corpus every implementation is asserted against.

## License

MIT OR Apache-2.0.
