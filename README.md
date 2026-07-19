# J5ML

[![CI](https://github.com/nekoyoubi/j5ml/actions/workflows/ci.yml/badge.svg)](https://github.com/nekoyoubi/j5ml/actions/workflows/ci.yml)
[![crates.io](https://img.shields.io/crates/v/j5ml?label=crates.io)](https://crates.io/crates/j5ml)
[![npm](https://img.shields.io/npm/v/%40nxis%2Fj5ml?label=npm)](https://www.npmjs.com/package/@nxis/j5ml)
[![docs](https://img.shields.io/badge/docs-j5ml.dev-blue)](https://j5ml.dev)
[![license](https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-green)](#license)

A JSON representation of a markup tree, and a family of implementations that agree on it.

```json
["div", { "class": "panel" }, "Hello, ", ["em", {}, "world"]]
```

Markup describes trees. JSON describes data. J5ML is for documents that do both at once: a tree whose nodes carry typed, structured values, with no second format smuggled through a string.

Documents are written in JSON or [JSON5](https://json5.org), interchangeably. The 5 in the name is that: a `.j5ml` file may carry comments, trailing commas, and unquoted keys, because hand-authored trees are a first-class use of this format rather than a tolerated one.

It is [JsonML](http://www.jsonml.org/syntax/) with one grammar change and three guarantees. Every valid JsonML document is valid J5ML; the reverse is not.

## Why not JsonML as-is

JsonML states its own scope: it *"was designed for lossless roundtrip conversion between XML and JSON"* and *"was never intended to be the way that everything that could be expressed in XML should be expressed in JSON."*

That design center has one consequence that runs through everything: **in XML, an attribute value is text.** A tree that needs a node to carry a list, a record, or a number that is still a number on arrival gets two bad options: string-encode it and hand-parse it back out, or promote it to child elements and lose the line between a node's configuration and its contents.

J5ML takes JsonML at its word and widens the format for trees that are not XML.

## Departures from JsonML

Anything not listed here is JsonML unchanged.

### An attribute value may be any JSON value

JsonML's grammar enumerates the production:

```
attribute-value = string | number | 'true' | 'false' | 'null' ;
```

J5ML widens it:

```
attribute-value = value ;
value           = string | number | 'true' | 'false' | 'null' | array | object ;
```

| attribute value | JsonML | J5ML |
|---|---|---|
| `string` `number` `true` `false` `null` | yes | yes |
| array | no | **yes** |
| object | no | **yes** |

Arrays and objects nest arbitrarily.

## Guarantees

These are not grammar changes. They are promises about what survives the trip.

### Values keep their type

A number arrives as a number, a boolean as a boolean, a list as a list. J5ML does not coerce values to strings in transit, and an implementation that does is non-conforming. A consumer that renders to a text medium performs that coercion at its own edge, where the target representation is known.

String-coercion mid-pipeline is where structure quietly becomes text and the type is lost for good; it is also where a value slips past a check that only inspected strings.

### Names survive verbatim

Element and attribute names are case-sensitive and are never folded, normalized, or validated against a vocabulary. `Gauge`, `ratio_bind`, `data-bind`, and `svg:circle` all round-trip unchanged. A consumer targeting a case-insensitive medium folds them itself.

### JSON5 in, canonical JSON out

A conforming parser accepts JSON5. This costs nothing, because JSON is a strict subset of it: a JSON document parses identically, so there is no mode flag, no sniffing, and no question about what a given `.j5ml` file is.

```json5
// health panel
["Block", {
  title: 'Health',                 // unquoted key, single-quoted string
  border_style: { fg: 'Cyan' },
},
  ["Gauge", { percent: 62.5, }],   // trailing comma
]
```

A conforming serializer emits canonical JSON and never JSON5. The asymmetry is deliberate: anything a J5ML implementation writes is readable by a plain JSON parser, which keeps the format interchangeable instead of forked.

The cost: **comments and trailing commas do not survive a round trip.** They are authoring affordances, not document content, and a tool that reads a hand-written file and writes it back will strip them.

## What it looks like in practice

A UI component tree:

```json
["Gauge", { "style": { "fg": "Green", "bold": true }, "percent": 62.5 }]
```

A terminal widget tree:

```json
["Layout", { "direction": "Vertical", "constraints": ["Length:1", "Min:0"] }]
```

A chart node whose configuration is genuinely a data structure:

```json
["Chart", { "series": [{ "label": "cpu", "points": [1, 2, 3] }] }]
```

Under JsonML each of these has to encode its structure as a string and parse it back on the other side, or promote it to child elements and make the node's real children ambiguous. A `Gauge`'s style is a property of the gauge, not a child of it. J5ML keeps structure and configuration separate.

## What's here

| | |
|---|---|
| `spec/j5ml.md` | the format |
| `spec/conformance-tests.json` | the shared corpus every implementation is asserted against |
| `rust/` | `j5ml`, Rust, serde-based |
| `ts/` | `@nxis/j5ml`, TypeScript, JSON5-based |
| *(planned)* | C#, Python |

## The corpus is the specification

Prose drifts from code. Every implementation here is asserted against `spec/conformance-tests.json`, in three tiers: canonical JSON documents that must parse to a given tree and serialize back byte-exact, JSON5 sources that must parse and canonicalize to a given JSON output, and malformed documents that must be rejected. An implementation cannot diverge from the format without failing, and a change to the format is a change to the corpus that every implementation must then satisfy.

Where `spec/j5ml.md` and the corpus disagree, the corpus is authoritative and the prose is a bug.

## J5ML is a format, not a policy

J5ML does not define which element names are legal, which attributes an element may carry, what any name means, or whether a document is safe to render. A parser accepts any well-formed tree, including one naming elements a given consumer would refuse.

Consumers layer their own vocabulary and their own safety rules on top. **A consumer that renders J5ML into a medium where markup is executable must sanitize it first.** J5ML neither performs nor implies sanitization.

What an implementation does ship for that job is a traversal: `walk` visits a node and every descendant, parents before children, and hands the visitor the path to each one, so a consumer's own validator can reject a node and say where it was. The rules stay the consumer's; the walking does not have to be rewritten each time.

## Relationship to the `jsonml` crate

The Rust ecosystem already has [`jsonml`](https://crates.io/crates/jsonml), a faithful implementation of the original grammar, scalar attribute values included. If you are round-tripping XML, use that one. If your tree is not XML and its nodes carry data, use this one.

## License

Licensed under either of MIT or Apache-2.0, at your option.
