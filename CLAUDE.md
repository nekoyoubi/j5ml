# J5ML

A JSON representation of a markup tree: JsonML with one grammar change (an attribute value may be any JSON value, not just a scalar) and three guarantees (values keep their JSON type, names survive verbatim, JSON5 is accepted as authoring syntax).

This is a multi-language monorepo. One spec, one shared conformance corpus, N implementations asserted against it.

## Layout

| | |
|---|---|
| `spec/j5ml.md` | the format: grammar, departures from JsonML, guarantees, conformance requirements |
| `spec/conformance-tests.json` | the shared corpus every implementation is asserted against |
| `rust/` | the `j5ml` crate: serde-based, complete |
| `ts/` | the `@nxis/j5ml` npm package: JSON5-based, complete |
| `csharp/` | the `J5ml` NuGet package: hand-written JSON5 reader and canonical writer, complete |
| `python/` | planned |
| `site/` | Astro site for j5ml.dev |

## The corpus is the specification

`spec/conformance-tests.json` is authoritative. Where `spec/j5ml.md` and the corpus disagree, **the corpus wins and the prose is a bug.**

It has three tiers:

- **canonical**: a JSON document that must parse to a given tree and serialize back byte-exact
- **authoring**: a JSON5 source that must parse and canonicalize to a given JSON output
- **invalid**: a malformed document that must be rejected

Changing the format means changing the corpus, and then every implementation must satisfy it. Adding an implementation means running the corpus, not writing a parallel test suite. Do not add implementation-local tests that assert format behavior; put them in the corpus so every language is held to them.

**Numbers in the corpus are written without a fractional part when they are integral.** JavaScript serializes `100.0` as `100`, so a corpus entry containing `100.0` would be unsatisfiable by a JS implementation. This is not cosmetic; it is a cross-implementation constraint the corpus enforces.

**Attribute names are ordered on serialization, including keys nested inside an attribute value.** Rust gets this from `BTreeMap` and from `serde_json::Map` being a `BTreeMap` by default; other implementations must sort explicitly. Order by code point, not by a language's default string comparison: JavaScript's compares UTF-16 code units, which disagrees with UTF-8 byte order above U+FFFF, and names may hold any character a JSON string may hold.

## JSON5 is input-only

A conforming parser accepts JSON5. A conforming serializer emits canonical JSON and **never** JSON5. The asymmetry is deliberate: anything a J5ML implementation writes is readable by a plain JSON parser, which keeps the format interchangeable rather than forked.

Say this plainly in docs rather than letting someone discover it: **comments and trailing commas do not survive a round trip.** They are authoring affordances, not document content.

## Rust implementation

```
cd rust && cargo test
```

Public API:

- `from_str`: accepts JSON5, which subsumes JSON; the default entry point
- `from_json_str`: strict JSON only
- `to_string`: always emits canonical JSON

`Node` is `Element { name, attrs: BTreeMap<String, Value>, children }` or `Text(String)`. Attribute values are `serde_json::Value`. Attributes live in a `BTreeMap` so serialization is deterministic.

Parsing goes through the `json5` crate, which is a real serde `Deserializer` rather than a JSON5-to-JSON text conversion. Do not replace this with a preprocessing step.

`Node` and `Element` are deliberately **not** `#[non_exhaustive]`. The format's shape is closed by definition, so it buys nothing and costs consumers ergonomic construction.

## TypeScript implementation

```
cd ts && pnpm test
```

Public API:

- `parse`: accepts JSON5, which subsumes JSON; the default entry point
- `parseJson`: strict JSON only
- `stringify`: always emits canonical JSON
- `walk`: visits a node and its descendants, parents before children, with each node's path

A `Node` is `Element { name, attrs, children }` or `Text { text }`. That is the same shape `spec/conformance-tests.json` uses to record an expected tree, so the conformance harness compares against the corpus directly rather than rebuilding it.

**`canonical()` is not optional.** It sorts attribute names by code point before serializing, recursively through attribute values. Do not swap it for `Object.keys().sort()`: that orders by UTF-16 code unit, which disagrees with UTF-8 byte order above U+FFFF, and names may hold any character a JSON string may hold.

## C# implementation

```
dotnet test csharp/tests/J5ml.Tests/J5ml.Tests.csproj
```

There is no solution file, so this takes the project path and runs from the repo root. CI and `scripts/implementations.mjs` use the same command.

Public API on `J5mlDocument`:

- `Parse`: accepts JSON5, which subsumes JSON; the default entry point
- `ParseJson`: strict JSON only, via `JsonNode.Parse`, whose defaults already reject comments and trailing commas
- `Stringify`: always emits canonical JSON
- `Traverse`: visits a node and its descendants, parents before children, with each node's path

Named `Traverse` rather than `Walk` because `Walk` is the enum a visitor returns.

Attribute values are `System.Text.Json.Nodes.JsonNode`. Values read out of a parsed tree are `DeepClone`d on the way into an element, since a `JsonNode` tracks a parent and throws if it is attached in two places.

**Two pieces are hand-written on purpose, and neither should be swapped for a library call.**

`Json5Parser` reads JSON5 directly rather than rewriting it into JSON and delegating. A rewrite would have to reproduce string escaping and number formatting exactly to avoid altering the document in transit, and error positions would point at rewritten text. This mirrors the Rust crate's rule about the `json5` crate.

`CanonicalJson` writes the output instead of `JsonNode.ToJsonString`. System.Text.Json's encoders are configured with `UnicodeRange`s, which only describe the BMP, so **every character above U+FFFF is escaped as a surrogate pair** no matter which built-in encoder is chosen, `UnsafeRelaxedJsonEscaping` included. `serde_json` and `JSON.stringify` both write those characters literally, so delegating here would emit `"😀"` where the other implementations emit the character and break byte-exact round-tripping. The corpus pins this with `attribute names above U+FFFF are ordered by code point`.

Integers are kept as integers through the parser so serialization never grows a fractional part, and a number that came from a JSON parse is written back from `JsonElement.GetRawText()` so its original spelling survives.

## Format, not policy

J5ML does not define which element names are legal, what any name means, or whether a document is safe to render. A parser accepts any well-formed tree. Consumers layer their own vocabulary and safety rules on top, and a consumer rendering into a medium where markup is executable must sanitize first. Do not add validation, name folding, or a built-in element vocabulary to an implementation; those belong to consumers.

## Relationship to `jsonml`

The [`jsonml`](https://crates.io/crates/jsonml) crate is a faithful implementation of the original grammar and is the right choice for XML round-tripping. J5ML is not a competitor to it and should not be framed as one; the README points XML round-trippers at it deliberately. Keep that framing in any docs or copy.

## `site/src/lib/spec-headings.mjs` stays

This rehype plugin renests and re-slugs the inlined spec's headings. It looks like something `@xtyle/astro`'s `Markdown` component should replace, and it is not.

`Markdown` wraps `renderMarkdown(source)`, which takes no options: no heading offset, no id prefix, and no way to read the headings back out. The plugin exists to do all three. `spec/j5ml.md` is a standalone document that owns an `<h1>`, so inlining it under the page's own `Spec` section means dropping every heading two levels and prefixing each id to clear the page's slugs. The page's table of contents is then built from `getHeadings()`, which reports those transformed headings precisely because the plugin runs inside Astro's markdown pipeline.

Swapping in `Markdown` would render an `<h1>` mid-page, collide the spec's slugs with the page's, and drop every spec entry from the rail. Keep the plugin, and keep the `.spec pre.astro-code` rules in `global.css` that style the code fences it renders.

## Conventions

- Package manager for the site is **pnpm**. `cd site && pnpm install && pnpm build`.
- Dual licensed MIT OR Apache-2.0. New files inherit that; do not add per-file license headers.
- The site is zero-client-JS by design. Components take `static`.
