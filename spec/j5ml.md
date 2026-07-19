# J5ML

J5ML is a JSON representation of a markup tree. An element is an array whose first member is the element's name, whose optional second member is an object of attributes, and whose remaining members are children.

```json
["div", { "class": "panel" }, "Hello, ", ["em", {}, "world"]]
```

J5ML is [JsonML](http://www.jsonml.org/syntax/) with one grammar change and three guarantees. Every valid JsonML document is a valid J5ML document. The reverse is not.

## Lineage

JsonML states its own scope: it *"was designed for lossless roundtrip conversion between XML and JSON"* and *"was never intended to be the way that everything that could be expressed in XML should be expressed in JSON."*

That design center has one consequence that runs through everything: in XML, an attribute value is text. A tree that needs a node to carry a list, a record, or a number that is still a number on arrival gets two bad options: string-encode it and hand-parse it back out, or promote it to child elements and lose the line between a node's configuration and its contents.

J5ML is for trees that are not XML. Each departure is recorded below; anything not listed is JsonML unchanged.

## Departures from JsonML

### An attribute value may be any JSON value

JsonML's grammar enumerates the attribute-value production:

```
attribute-value = string | number | 'true' | 'false' | 'null' ;
```

J5ML widens it:

```
attribute-value = value ;
value           = string | number | 'true' | 'false' | 'null' | array | object ;
```

Arrays and objects nest arbitrarily.

```json
["Layout", { "constraints": ["Length:1", "Min:0", "Length:1"] }]
["Gauge",  { "style": { "fg": "Green", "bold": true }, "percent": 62.5 }]
["Chart",  { "series": [{ "label": "cpu", "points": [1, 2, 3] }] }]
```

Representing these as child elements is possible, and is the correct answer when the goal is XML fidelity. It is the wrong answer here: a `Gauge`'s style is a property of the gauge, not a child of it, and collapsing the two makes a node's real children ambiguous. J5ML keeps structure and configuration separate.

## Guarantees

These are not grammar changes. They constrain what a conforming implementation may do to a document in transit.

### Values keep their type

A number arrives as a number, a boolean as a boolean, a list as a list. An implementation must not coerce a value to a string while parsing, holding, or serializing a tree. A consumer that renders to a text medium performs that coercion at its own edge, where the target representation is known.

An implementation that stringifies values in transit is non-conforming even when the resulting text looks correct: type information lost mid-pipeline cannot be recovered, and a value that has become a string can pass a check that only inspects strings.

### Names survive verbatim

See *Names*, below. No folding, no normalization, no vocabulary validation.

### JSON5 in, canonical JSON out

A J5ML document may be written in JSON or in [JSON5](https://json5.org). A conforming parser accepts JSON5.

This costs nothing, because JSON is a strict subset of JSON5: a JSON document parses identically under a JSON5 parser, so there is no mode to select, no sniffing, and no ambiguity about what a given file is. A `.j5ml` file is read the same way regardless of which syntax it was written in.

What that buys an author is the syntax JSON withholds:

```json5
// health panel
["Block", {
  title: 'Health',                 // unquoted key, single-quoted string
  border_style: { fg: 'Cyan' },
},
  ["Gauge", { percent: 62.5, }],   // trailing comma
]
```

Comments, trailing commas, unquoted keys, and single-quoted strings are all legal input. Hand-authored trees are a first-class use of this format, not a tolerated one, and JSON is a hostile syntax to write by hand.

**Serialization is always canonical JSON.** A conforming serializer never emits JSON5 syntax. This is what keeps the format interchangeable rather than forked: any consumer, including one whose parser is JSON-only, can read what a J5ML implementation writes.

The consequence: **comments and trailing commas do not survive a round trip.** They are authoring affordances, not document content. A tool that parses a hand-written file and writes it back will strip them. Author in files that tools read but do not rewrite, or accept the loss.

An implementation may also offer a strict parser that refuses JSON5 and accepts only canonical JSON, for the case where a document is expected to already be canonical and authoring syntax should be an error. That is an addition, not a substitute: the default parser accepts JSON5.

## Grammar

```
element    = [ name ] | [ name, attributes ] | [ name, ...children ]
           | [ name, attributes, ...children ]
name       = string          ; non-empty
attributes = object          ; keys are attribute names
children   = element | text
text       = string
value      = string | number | boolean | null | array | object
```

An element is an array whose first member is a non-empty string. If the second member is an object it is the attribute map; otherwise every member after the first is a child. A member that is a string is a text node; a member that is an array is a nested element.

J5ML inherits JsonML's disambiguation rule deliberately: an object in the second position is always attributes, never a child, because a child is either a string or an array.

## Names

Element and attribute names are **case-sensitive** and are preserved verbatim. J5ML does not lowercase, normalize, or validate them against any vocabulary. A name may carry a namespace prefix (`"svg:circle"`), a hyphen (`"data-bind"`), an underscore (`"ratio_bind"`), or any other character a JSON string may hold.

Consumers that target a case-insensitive medium, such as HTML, fold names themselves. J5ML preserves what was written.

## Numbers

JSON does not specify numeric precision. J5ML requires 64-bit doubles. This is normative rather than descriptive: the conformance corpus asserts byte-exact serialization, and an implementation using arbitrary precision or decimals would emit a different document for the same input.

Integers are exact to 2^53. Fractions are not: `0.1` has no exact binary representation, and no double-based format can promise otherwise. What a conforming serializer must emit is the shortest decimal that parses back to the same double, so a value survives parse and serialize unchanged even though it was never exact to begin with.

One consequence bites across languages: a number whose value is an integer is written without a fractional part. `100`, not `100.0`. Implementations disagree about the latter, and the corpus holds every implementation to the former.

## Ordering

Attribute names are ordered on serialization, by Unicode code point. The rule applies to every object a document contains: the attribute map itself, and any object nested inside an attribute value, however deep.

This is normative rather than descriptive, for the same reason numeric form is: the conformance corpus asserts byte-exact serialization, so two implementations that ordered differently would fail each other's cases while each believed it was correct. Attribute order carries no meaning in a document, which is precisely why it has to be pinned. Nothing in the tree would otherwise settle it.

Order by code point, not by a language's default string comparison. The two agree below U+FFFF and part company above it: JavaScript compares UTF-16 code units, which sorts an astral-plane name before one in the private use area, where code-point order puts it after. A name may hold any character a JSON string may hold, so the distinction is reachable rather than theoretical. Rust's `BTreeMap<String, _>` orders by UTF-8 bytes, which preserves code-point order and is already correct.

Parsing imposes no ordering requirement. A document is well-formed whatever order its attributes arrive in; the requirement is on what a serializer emits.

## What J5ML is not

J5ML is a **format**, not a policy. It does not define:

- which element names are legal
- which attributes an element may carry
- what any name means
- whether a document is safe to render

Those are a consumer's concern. A consumer that renders J5ML into a document where markup is executable must sanitize it; J5ML neither performs nor implies sanitization. A parser accepts any well-formed tree, including one naming elements a given consumer would refuse.

## Conformance

`spec/conformance-tests.json` is the shared corpus. Every implementation in this repository is asserted against it, so an implementation cannot diverge from the format without failing. The corpus is the specification's executable half; where this prose and the corpus disagree, the corpus is authoritative and the prose is a bug.

Each case is:

```json
{
  "name": "unique identifier",
  "json": "the J5ML document, as a JSON string",
  "tree": { "...": "the expected parse, in the corpus's neutral tree encoding" },
  "roundTrips": true
}
```

`roundTrips` records whether serializing the parsed tree reproduces `json` byte-for-byte. It is `false` for inputs that are well-formed but not canonical, such as an element carrying an empty attribute object; those cases carry a `serializesTo` instead.

The corpus carries three sets:

- **`cases`**: canonical JSON; parse to the given tree, then serialize byte-exact
- **`authoring`**: JSON5 sources; parse, then serialize to the canonical JSON recorded in `serializesTo`, and an implementation that does not accept JSON5 fails these
- **`invalid`**: documents a conforming parser must reject, each with the reason it fails
