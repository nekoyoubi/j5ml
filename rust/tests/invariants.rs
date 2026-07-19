//! Guarantees the shared corpus cannot express: it records documents, not the
//! behavior of a builder API.

#[test]
fn an_empty_name_cannot_be_serialized() {
    let built = j5ml::Node::Element(j5ml::Element::new(""));
    assert!(j5ml::to_string(&built).is_err());

    let defaulted = j5ml::Node::Element(j5ml::Element::default());
    assert!(j5ml::to_string(&defaulted).is_err());
}

#[test]
fn a_built_element_round_trips() {
    let built = j5ml::Node::Element(
        j5ml::Element::new("Gauge")
            .attr("percent", 62.5)
            .child("ready"),
    );
    let json = j5ml::to_string(&built).unwrap();
    assert_eq!(j5ml::from_str(&json).unwrap(), built);
}

#[test]
fn walk_reports_the_path_of_each_node() {
    let tree = j5ml::from_str(r#"["a",{},["b",{},"one"],["c",{}]]"#).unwrap();

    let mut seen = Vec::new();
    tree.walk(&mut |node, path| {
        let label = match node {
            j5ml::Node::Element(element) => element.name.clone(),
            j5ml::Node::Text(text) => text.clone(),
        };
        seen.push(format!("{path}:{label}"));
        j5ml::Walk::Continue
    });

    assert_eq!(seen, [":a", "/0:b", "/0/0:one", "/1:c"]);
}

#[test]
fn walk_stops_and_skips_on_request() {
    let tree = j5ml::from_str(r#"["a",{},["b",{},"deep"],["c",{}]]"#).unwrap();

    let mut skipped = Vec::new();
    tree.walk(&mut |node, _| match node.as_element().map(|e| e.name.as_str()) {
        Some("b") => j5ml::Walk::SkipChildren,
        _ => {
            skipped.push(node.as_element().map(|e| e.name.clone()));
            j5ml::Walk::Continue
        }
    });
    assert_eq!(skipped.len(), 2, "b's text child must not be visited");

    let mut visited = 0;
    tree.walk(&mut |_, _| {
        visited += 1;
        j5ml::Walk::Stop
    });
    assert_eq!(visited, 1, "Stop must end the traversal immediately");
}
