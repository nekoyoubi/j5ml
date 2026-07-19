#![doc = include_str!("../README.md")]
#![forbid(unsafe_code)]

use std::collections::BTreeMap;
use std::fmt;

use serde::de::{self, SeqAccess, Visitor};
use serde::ser::{self, SerializeSeq};
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use serde_json::{Map, Value};

/// A node in a J5ML tree: either an element or a text node.
#[derive(Debug, Clone, PartialEq)]
pub enum Node {
    /// An element: a name, an attribute map, and children.
    Element(Element),
    /// A text node.
    Text(String),
}

/// An element: `["name", { attributes }, ...children]`.
///
/// Names are preserved verbatim. J5ML does not lowercase, normalize, or validate
/// them; a consumer targeting a case-insensitive medium folds them itself.
#[derive(Debug, Clone, PartialEq, Default)]
pub struct Element {
    /// The element name, exactly as authored.
    pub name: String,
    /// Attributes, ordered by name so serialization is deterministic.
    pub attrs: BTreeMap<String, Value>,
    /// Child nodes, in document order.
    pub children: Vec<Node>,
}

impl Element {
    /// Builds an element with no attributes and no children.
    pub fn new(name: impl Into<String>) -> Self {
        Element {
            name: name.into(),
            attrs: BTreeMap::new(),
            children: Vec::new(),
        }
    }

    /// Sets an attribute, returning the element for chaining.
    pub fn attr(mut self, name: impl Into<String>, value: impl Into<Value>) -> Self {
        self.attrs.insert(name.into(), value.into());
        self
    }

    /// Appends a child, returning the element for chaining.
    pub fn child(mut self, node: impl Into<Node>) -> Self {
        self.children.push(node.into());
        self
    }
}

impl From<Element> for Node {
    fn from(element: Element) -> Self {
        Node::Element(element)
    }
}

impl From<String> for Node {
    fn from(text: String) -> Self {
        Node::Text(text)
    }
}

impl From<&str> for Node {
    fn from(text: &str) -> Self {
        Node::Text(text.to_string())
    }
}

impl Node {
    /// The element, if this node is one.
    pub fn as_element(&self) -> Option<&Element> {
        match self {
            Node::Element(element) => Some(element),
            _ => None,
        }
    }

    /// The text, if this node is a text node.
    pub fn as_text(&self) -> Option<&str> {
        match self {
            Node::Text(text) => Some(text),
            _ => None,
        }
    }

    /// Visits this node and every descendant, parents before children.
    ///
    /// The visitor receives each node's [`Path`] and decides whether traversal
    /// continues. This is the primitive a consumer builds validation or
    /// sanitization on: J5ML does not define which names are legal or whether a
    /// tree is safe to render, so those checks belong to the consumer that knows
    /// its output medium. The path is what lets a rejection say *where*.
    ///
    /// ```
    /// use j5ml::{from_str, Walk};
    ///
    /// let tree = from_str(r#"["doc",{},["script",{},"x"]]"#).unwrap();
    /// let mut found = None;
    /// tree.walk(&mut |node, path| {
    ///     if node.as_element().is_some_and(|e| e.name == "script") {
    ///         found = Some(path.to_string());
    ///         return Walk::Stop;
    ///     }
    ///     Walk::Continue
    /// });
    /// assert_eq!(found.as_deref(), Some("/0"));
    /// ```
    pub fn walk(&self, visit: &mut impl FnMut(&Node, Path<'_>) -> Walk) {
        let mut trail = Vec::new();
        self.walk_from(&mut trail, visit);
    }

    fn walk_from(
        &self,
        trail: &mut Vec<usize>,
        visit: &mut impl FnMut(&Node, Path<'_>) -> Walk,
    ) -> Walk {
        match visit(self, Path(trail.as_slice())) {
            Walk::Stop => return Walk::Stop,
            Walk::SkipChildren => return Walk::Continue,
            Walk::Continue => {}
        }

        if let Node::Element(element) = self {
            for (index, child) in element.children.iter().enumerate() {
                trail.push(index);
                let outcome = child.walk_from(trail, visit);
                trail.pop();
                if outcome == Walk::Stop {
                    return Walk::Stop;
                }
            }
        }

        Walk::Continue
    }
}

/// Whether a traversal continues past the node just visited.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Walk {
    /// Descend into this node's children, then carry on.
    Continue,
    /// Leave this node's children unvisited, then carry on with its siblings.
    SkipChildren,
    /// End the traversal.
    Stop,
}

/// Where a node sits in a tree: the child indices leading to it from the root.
///
/// Attributes are not part of a path. A consumer reporting a bad attribute
/// appends the attribute name to the element's path itself, because only the
/// consumer knows what it is rejecting.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Path<'a>(&'a [usize]);

impl Path<'_> {
    /// The child indices, outermost first.
    pub fn indices(&self) -> &[usize] {
        self.0
    }

    /// How many levels below the root this node sits.
    pub fn depth(&self) -> usize {
        self.0.len()
    }

    /// Whether this is the document's root node.
    pub fn is_root(&self) -> bool {
        self.0.is_empty()
    }
}

/// Renders as `/0/2`, and as the empty string at the root.
impl fmt::Display for Path<'_> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        for index in self.0 {
            write!(f, "/{index}")?;
        }
        Ok(())
    }
}

/// Parses a J5ML document.
///
/// Accepts JSON5, which subsumes JSON: comments, trailing commas, unquoted keys,
/// and single-quoted strings all parse, and a plain JSON document parses
/// identically. There is no mode to select.
pub fn from_str(source: &str) -> Result<Node, Error> {
    json5::from_str(source).map_err(|e| Error(ErrorKind::Parse(e.to_string())))
}

/// Parses a J5ML document, rejecting anything JSON would reject.
///
/// Use when a document is expected to already be canonical and JSON5 authoring
/// syntax should be an error rather than an accepted input.
pub fn from_json_str(source: &str) -> Result<Node, Error> {
    serde_json::from_str(source).map_err(|e| Error(ErrorKind::Parse(e.to_string())))
}

/// Serializes a J5ML document to canonical JSON.
///
/// Never emits JSON5 syntax. Comments and trailing commas in a parsed source do
/// not survive, because they are authoring affordances rather than document
/// content.
pub fn to_string(node: &Node) -> Result<String, Error> {
    serde_json::to_string(node).map_err(|e| Error(ErrorKind::Serialize(e.to_string())))
}

#[derive(Debug)]
enum ErrorKind {
    Parse(String),
    Serialize(String),
}

/// A J5ML parse or serialization failure.
#[derive(Debug)]
pub struct Error(ErrorKind);

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match &self.0 {
            ErrorKind::Parse(message) => write!(f, "J5ML parse failed: {message}"),
            ErrorKind::Serialize(message) => write!(f, "J5ML serialization failed: {message}"),
        }
    }
}

impl std::error::Error for Error {}

impl Serialize for Node {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        match self {
            Node::Text(text) => serializer.serialize_str(text),
            Node::Element(element) => {
                if element.name.is_empty() {
                    return Err(ser::Error::custom(NAME_REQUIRED));
                }
                let has_attrs = !element.attrs.is_empty();
                let len = 1 + usize::from(has_attrs) + element.children.len();
                let mut seq = serializer.serialize_seq(Some(len))?;
                seq.serialize_element(&element.name)?;
                if has_attrs {
                    seq.serialize_element(&element.attrs)?;
                }
                for child in &element.children {
                    seq.serialize_element(child)?;
                }
                seq.end()
            }
        }
    }
}

struct NodeVisitor;

impl<'de> Visitor<'de> for NodeVisitor {
    type Value = Node;

    fn expecting(&self, f: &mut fmt::Formatter) -> fmt::Result {
        f.write_str("a J5ML element array or a text string")
    }

    fn visit_str<E: de::Error>(self, value: &str) -> Result<Node, E> {
        Ok(Node::Text(value.to_string()))
    }

    fn visit_seq<A>(self, mut seq: A) -> Result<Node, A::Error>
    where
        A: SeqAccess<'de>,
    {
        let mut members = Vec::new();
        while let Some(member) = seq.next_element::<Value>()? {
            members.push(member);
        }

        element_from_members::<A::Error>(members).map(Node::Element)
    }
}

const NAME_REQUIRED: &str = "an element's first member must be a non-empty name";

fn into_attrs(map: Map<String, Value>) -> BTreeMap<String, Value> {
    map.into_iter().collect()
}

fn element_from_members<E: de::Error>(members: Vec<Value>) -> Result<Element, E> {
    let mut members = members.into_iter();

    let name = match members.next() {
        Some(Value::String(name)) if !name.is_empty() => name,
        _ => return Err(E::custom(NAME_REQUIRED)),
    };

    let mut attrs = BTreeMap::new();
    let mut children = Vec::new();

    // The second member is attributes only when it is an object; a child is
    // always a string or an array, so the two can never be confused.
    if let Some(second) = members.next() {
        match second {
            Value::Object(map) => attrs = into_attrs(map),
            other => children.push(value_to_node::<E>(other)?),
        }
    }

    for member in members {
        children.push(value_to_node::<E>(member)?);
    }

    Ok(Element {
        name,
        attrs,
        children,
    })
}

fn value_to_node<E: de::Error>(value: Value) -> Result<Node, E> {
    match value {
        Value::String(text) => Ok(Node::Text(text)),
        Value::Array(members) => element_from_members::<E>(members).map(Node::Element),
        _ => Err(E::custom("a child member must be a string or an array")),
    }
}

impl<'de> Deserialize<'de> for Node {
    fn deserialize<D>(deserializer: D) -> Result<Node, D::Error>
    where
        D: Deserializer<'de>,
    {
        deserializer.deserialize_any(NodeVisitor)
    }
}
