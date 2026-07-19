//! Asserts this implementation against the shared corpus.
//!
//! The corpus is authoritative. A case that fails here is either a bug in this
//! implementation or a change to the format that every other implementation in
//! the repository must also make.

use std::collections::BTreeMap;

use j5ml::{from_json_str, from_str, to_string, Element, Node};
use serde_json::Value;

// `spec/conformance-tests.json` lives outside this crate, so it cannot be
// packaged. The fixture is a copy; `fixture_matches_the_shared_corpus` fails if
// the two drift, and skips when the published crate is extracted on its own.
const CORPUS: &str = include_str!("fixtures/conformance-tests.json");

fn corpus() -> Value {
    serde_json::from_str(CORPUS).expect("the conformance corpus must be valid JSON")
}

/// Rebuilds a node from the corpus's neutral tree encoding so the comparison is
/// against the corpus rather than against this implementation's own parse.
fn expected_node(spec: &Value) -> Node {
    if let Some(text) = spec.get("text") {
        return Node::Text(
            text.as_str()
                .expect("a text node's value must be a string")
                .to_string(),
        );
    }

    let name = spec
        .get("name")
        .and_then(Value::as_str)
        .expect("an element must carry a name")
        .to_string();

    let mut attrs = BTreeMap::new();
    if let Some(Value::Object(map)) = spec.get("attrs") {
        for (key, value) in map {
            attrs.insert(key.clone(), value.clone());
        }
    }

    let children = spec
        .get("children")
        .and_then(Value::as_array)
        .map(|list| list.iter().map(expected_node).collect())
        .unwrap_or_default();

    Node::Element(Element {
        name,
        attrs,
        children,
    })
}

#[test]
fn parses_every_corpus_case_to_the_expected_tree() {
    let corpus = corpus();
    let cases = corpus["cases"].as_array().expect("cases must be an array");
    assert!(!cases.is_empty(), "the corpus must not be empty");

    for case in cases {
        let name = case["name"].as_str().unwrap();
        let source = case["json"].as_str().unwrap();

        let parsed = from_str(source).unwrap_or_else(|e| panic!("{name}: parse failed: {e}"));
        let expected = expected_node(&case["tree"]);

        assert_eq!(parsed, expected, "{name}: parsed tree differs from corpus");
    }
}

#[test]
fn serializes_every_corpus_case_as_the_corpus_records() {
    let corpus = corpus();

    for case in corpus["cases"].as_array().unwrap() {
        let name = case["name"].as_str().unwrap();
        let source = case["json"].as_str().unwrap();
        let parsed = from_str(source).unwrap();
        let serialized = to_string(&parsed).unwrap_or_else(|e| panic!("{name}: serialize: {e}"));

        let round_trips = case["roundTrips"].as_bool().unwrap_or(false);
        if round_trips {
            assert_eq!(serialized, source, "{name}: expected a byte-exact round trip");
        } else {
            let expected = case["serializesTo"]
                .as_str()
                .unwrap_or_else(|| panic!("{name}: a non-round-tripping case needs serializesTo"));
            assert_eq!(serialized, expected, "{name}: serialized form differs");
        }
    }
}

#[test]
fn reparsing_a_serialized_tree_is_stable() {
    let corpus = corpus();

    for case in corpus["cases"].as_array().unwrap() {
        let name = case["name"].as_str().unwrap();
        let parsed = from_str(case["json"].as_str().unwrap()).unwrap();
        let reparsed = from_str(&to_string(&parsed).unwrap()).unwrap();

        assert_eq!(parsed, reparsed, "{name}: reparse is not stable");
    }
}

#[test]
fn accepts_every_authoring_corpus_case_and_canonicalizes_it() {
    let corpus = corpus();
    let authoring = corpus["authoring"]
        .as_array()
        .expect("authoring must be present");
    assert!(!authoring.is_empty(), "the authoring set must not be empty");

    for case in authoring {
        let name = case["name"].as_str().unwrap();
        let source = case["json5"].as_str().unwrap();
        let expected = case["serializesTo"].as_str().unwrap();

        let parsed =
            from_str(source).unwrap_or_else(|e| panic!("{name}: JSON5 source must parse: {e}"));
        let serialized = to_string(&parsed).unwrap();

        assert_eq!(serialized, expected, "{name}: did not canonicalize as recorded");
    }
}

#[test]
fn the_strict_parser_refuses_json5_authoring_syntax() {
    let corpus = corpus();

    for case in corpus["authoring"].as_array().unwrap() {
        let name = case["name"].as_str().unwrap();
        let source = case["json5"].as_str().unwrap();
        let expected = case["serializesTo"].as_str().unwrap();

        // A case whose source is already canonical JSON is legal under both parsers;
        // only the ones carrying real JSON5 syntax must be refused by the strict one.
        if source == expected {
            assert!(
                from_json_str(source).is_ok(),
                "{name}: canonical source must parse strictly"
            );
        } else {
            assert!(
                from_json_str(source).is_err(),
                "{name}: the strict parser must refuse JSON5 syntax"
            );
        }
    }
}

#[test]
fn rejects_every_invalid_corpus_case() {
    let corpus = corpus();
    let invalid = corpus["invalid"].as_array().expect("invalid must be present");
    assert!(!invalid.is_empty(), "the invalid set must not be empty");

    for case in invalid {
        let name = case["name"].as_str().unwrap();
        let source = case["json"].as_str().unwrap();

        assert!(
            from_str(source).is_err(),
            "{name}: expected a parse failure ({})",
            case["reason"].as_str().unwrap_or("no reason recorded")
        );
    }
}

/// Skips when the crate is extracted on its own, where `../spec` does not exist.
#[test]
fn fixture_matches_the_shared_corpus() {
    let shared =
        std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../spec/conformance-tests.json");
    let Ok(shared) = std::fs::read_to_string(&shared) else {
        return;
    };

    let normalize = |text: &str| text.replace('\r', "");

    assert_eq!(
        normalize(&shared),
        normalize(CORPUS),
        "rust/tests/fixtures/conformance-tests.json has drifted from spec/conformance-tests.json"
    );
}
