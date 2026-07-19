// Mirrors the install sample on j5ml.dev so the site cannot publish code that does not run.
#[test]
fn the_install_sample_compiles_and_passes() -> Result<(), j5ml::Error> {
    use j5ml::{from_str, to_string};

    // Accepts JSON5, which subsumes JSON.
    let node = from_str("['Gauge', { percent: 62.5, }]")?;

    let gauge = node.as_element().unwrap();
    assert_eq!(gauge.name, "Gauge");
    assert_eq!(gauge.attrs["percent"], 62.5);

    // Always writes canonical JSON.
    assert_eq!(to_string(&node)?, r#"["Gauge",{"percent":62.5}]"#);
    Ok(())
}
