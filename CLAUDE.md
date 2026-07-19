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
| `csharp/` `python/` | planned; the directories are placeholders |
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

## Format, not policy

J5ML does not define which element names are legal, what any name means, or whether a document is safe to render. A parser accepts any well-formed tree. Consumers layer their own vocabulary and safety rules on top, and a consumer rendering into a medium where markup is executable must sanitize first. Do not add validation, name folding, or a built-in element vocabulary to an implementation; those belong to consumers.

## Relationship to `jsonml`

The [`jsonml`](https://crates.io/crates/jsonml) crate is a faithful implementation of the original grammar and is the right choice for XML round-tripping. J5ML is not a competitor to it and should not be framed as one; the README points XML round-trippers at it deliberately. Keep that framing in any docs or copy.

## Revisit when xtyle 0.9 lands

Three things here are workarounds for `@xtyle/astro@0.8.0` and should be deleted, not carried forward:

- `patches/@xtyle__astro@0.8.0.patch`: swaps `resolveAlgorithm` for `getAlgorithm` so generated icon marks bake (the published package omits the `algorithms/` directory the former reads), and adds a `static` prop to `Hero` so it stops loading the element runtime for a pure-CSS layout. Both are fixed upstream or unnecessary once the version moves; drop the patch and the `patchedDependencies` entry in `pnpm-workspace.yaml`.
- `site/src/lib/spec-headings.mjs`: renests and re-slugs the inlined spec's headings. **0.9 is expected to ship an `<xtyle-markdown>` component**, which is what this hand-rolls; prefer it and delete the plugin plus the `.spec pre.astro-code` rules in `global.css`.
- The `data-toc-link` indent selectors in `global.css`. `TocItem` is flat in 0.8, so subsection hierarchy is painted on with CSS; if `Toc` gains a `level`, hand `getHeadings()` through instead.

## Conventions

- Package manager for the site is **pnpm**. `cd site && pnpm install && pnpm build`.
- Dual licensed MIT OR Apache-2.0. New files inherit that; do not add per-file license headers.
- The site is zero-client-JS by design. Components take `static`.
