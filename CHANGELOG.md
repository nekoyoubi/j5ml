# Changelog

## v0.2.0

A third implementation, the corpus case that had to exist before it could be trusted, and the release plumbing to publish all three.

### C#

- **`J5ml` on NuGet, asserted against the same corpus.** `Parse` accepts JSON5, `ParseJson` refuses it, `Stringify` writes canonical JSON, and `Traverse` walks a tree parents-first handing each node its path. The surface is the one the other two already have
  - attribute values are `System.Text.Json.Nodes.JsonNode`, so any JSON value is legal and keeps its type
  - values are `DeepClone`d into an element, because a `JsonNode` tracks a parent and refuses to be attached twice
- **The JSON5 reader is hand-written, not a rewrite that delegates.** Turning JSON5 into JSON first would have to reproduce string escaping and number formatting exactly to avoid altering the document on the way through, and a syntax error's position would point at the rewritten text instead of at what was authored
- **The serializer is hand-written too, and it had to be.** System.Text.Json configures its encoders with `UnicodeRange`s, which describe only the Basic Multilingual Plane, so every character above U+FFFF is escaped as a surrogate pair under every built-in encoder including `UnsafeRelaxedJsonEscaping`. Rust and TypeScript both emit those characters literally, so delegating would have written `"\ud83d\ude00"` where they write `"😀"`, and the document would no longer round-trip

### The corpus

- **Added a case for attribute names above U+FFFF.** The ordering rule was previously asserted only in the TypeScript package's own tests, which is exactly the thing the corpus exists to prevent: C# passed all 110 conformance cases while emitting different bytes than the other two for any document carrying an emoji
  - the case pins both halves at once, since it orders an astral name against a BMP one and expects the astral character back out unescaped

### Packaging

- **The npm package is `@nxis/j5ml`.** npm refuses `j5ml` unscoped: it sits close enough to `jsml` and `json5` to trip the similarity check, and every near variant fails the same test, so no unscoped name was going to clear it. The crate stays `j5ml` and the NuGet package is `J5ml`, since neither registry has that check
  - `import { parse } from "j5ml"` becomes `import { parse } from "@nxis/j5ml"`; nothing else about the package moved
- **Publishing is automated for all three registries.** Cutting a release fires one workflow per registry, so nothing is pushed by hand: npm over OIDC, crates.io and NuGet from stored tokens
- **`pnpm release` gates a release before it can happen.** It refuses a version mismatch between the three manifests, a dirty tree, a branch that is not `main`, an unpushed commit, an existing tag, or a missing changelog section, then runs every suite before tagging. `--dry-run` walks the gates without tagging
- **`pnpm version:bump` writes one version across all three manifests**, which is the agreement the release gate then checks
- **Both release scripts read one list of implementations** rather than each naming the languages itself. Adding C# to a pair of scripts that knew about two would have left the third manifest behind on the next bump, and a gate comparing two versions cannot see a third disagree: the tag would have gone out with npm and crates.io on the new version and NuGet refusing a version it had already published. The same shape as the corpus gap, one layer out
- **CI runs the TypeScript and C# suites** on every push and pull request. Rust stays a local gate

### The site

- **Moved to xtyle 0.9 and deleted the patch.** `@xtyle/core` ships its `algorithms` directory now, so generated icon marks bake unaided, and `Hero` takes a `static` prop upstream
- **Table-of-contents hierarchy is structural.** `TocItem` carries a `level`, so `Toc` emits real nested lists instead of a rail indented by a hand-listed set of ids in CSS
- **The crates.io button moved off the `soft` variant**, which 0.9 no longer accepts and had been quietly falling back to `solid`
- **Every registry is linked from the toolbar and badged in its README.** crates.io, npm, and NuGet each get a mark in the masthead and a version badge on the package that ships there
- **Pages builds from the workflow rather than from the repository root**, so the published site is `site/dist` and nothing else in the repository is served

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
