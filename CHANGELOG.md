# Changelog

## v0.1.0

In XML an attribute value is text, so in JsonML's grammar an attribute value is text. J5ML widens that one production and spends the rest of its effort on what happens afterward: what an implementation may and may not do to a document in transit, and a corpus that fails an implementation that gets it wrong.

This is the first cut. One spec, one corpus, two implementations.

### The format

- **An attribute value may be any JSON value.** JsonML's grammar enumerates `string`, `number`, `true`, `false`, and `null`; J5ML admits arrays and objects, nesting arbitrarily. A node whose configuration is genuinely a data structure (a constraint list, a style record, a chart series) gets to keep it as one, instead of string-encoding it and hand-parsing it back out, or promoting it to child elements and making its real children ambiguous
  - a `Gauge`'s style is a property of the gauge, not a child of it, and a grammar that cannot tell the difference forces you to pick which one to lie about
- **Values keep their type.** A number arrives as a number, a list as a list. An implementation that coerces to string in transit is non-conforming even when the resulting text looks correct: type lost mid-pipeline does not come back, and a value that has become a string can pass a check that only inspects strings
- **Names survive verbatim.** Case-sensitive, never folded, never validated against a vocabulary. `Gauge`, `ratio_bind`, `data-bind`, and `svg:circle` all round-trip unchanged, and a consumer targeting a case-insensitive medium folds them at its own edge
- **JSON5 in, canonical JSON out.** A conforming parser accepts JSON5, which costs nothing because JSON is a strict subset of it, so there is no mode flag and no sniffing. A conforming serializer never emits JSON5, and the asymmetry is deliberate: anything a J5ML implementation writes is readable by a plain JSON parser
  - comments and trailing commas do not survive a round trip; they are authoring affordances, not document content, and a tool that reads a hand-written file and writes it back will strip them
- **It is a format, not a policy.** J5ML does not define which names are legal, what any of them mean, or whether a document is safe to render. A consumer rendering into a medium where markup is executable sanitizes it first; J5ML neither performs nor implies sanitization

### The corpus

- **`spec/conformance-tests.json` is the specification's executable half.** Where the prose and the corpus disagree, the corpus wins and the prose is a bug. Three tiers: canonical documents that parse to a given tree and serialize back byte-exact, JSON5 sources that canonicalize to a recorded output, and malformed documents that must be rejected
- **It earned that authority before the TypeScript implementation was written.** JavaScript serializes `100.0` as `100`, so any corpus entry written with a fractional part would have been unsatisfiable the moment a JavaScript implementation arrived. Every integral number in the corpus is written without one, and every implementation is held to it

### Rust

- **Rust goes first.** `from_str` accepts JSON5, `from_json_str` refuses it, and `to_string` always writes canonical JSON. A `Node` is an element or a text node; attributes live in a `BTreeMap`, so serialization is deterministic rather than incidentally ordered
- **Parsing goes through a real serde deserializer**, not a JSON5-to-JSON text conversion, so authoring syntax is handled by the parser instead of a preprocessing pass
- **Asserted against the shared corpus** rather than a parallel test suite of its own, which is what keeps a second implementation from quietly forking the format
- **`walk` is the traversal a consumer builds its own checks on.** It visits parents before children and hands each node its `Path`, so a validator that rejects something can say where it was; `SkipChildren` leaves a subtree alone and `Stop` ends at the first finding. J5ML defines no vocabulary and no safety rules, but it should not make everyone rewrite the walking

### TypeScript

- **TypeScript goes second, against the same corpus.** `parse` accepts JSON5, `parseJson` refuses it, and `stringify` always writes canonical JSON. A node is `{name, attrs, children}` or `{text}`, the same shape the corpus uses to record a tree, with `isElement` and `isText` to narrow one
- **Attribute names are sorted by code point**, including keys nested inside an attribute value. JavaScript's default comparison orders by UTF-16 code unit, which disagrees with UTF-8 byte order above U+FFFF, so two implementations sorting the obvious way would emit different bytes for the same tree
- **The corpus grew to answer it.** Two cases now pin attribute ordering; it had gone unasserted because the only multi-attribute document in the corpus happened to be written in sorted order already
- **`walk` matches Rust's**, down to the three-state return, so a check written against one reads the same against the other
