/**
 * J5ML: a JSON representation of a markup tree.
 *
 * The tree encoding here is the same one the shared conformance corpus uses:
 * an element is `{name, attrs, children}` and a text node is `{text}`.
 */

import JSON5 from "json5";

export type JsonValue =
	| string
	| number
	| boolean
	| null
	| JsonValue[]
	| { [key: string]: JsonValue };

/** An element: `["name", { attributes }, ...children]`. */
export interface Element {
	/** The element name, exactly as authored. */
	name: string;
	/** Attributes. Any JSON value is a legal attribute value. */
	attrs: Record<string, JsonValue>;
	/** Child nodes, in document order. */
	children: Node[];
}

/** A text node. */
export interface Text {
	text: string;
}

export type Node = Element | Text;

/** A J5ML parse or serialization failure. */
export class J5mlError extends Error {
	constructor(message: string) {
		super(message);
		this.name = "J5mlError";
	}
}

const NAME_REQUIRED = "an element's first member must be a non-empty name";
const DOCUMENT_SHAPE = "a document is an element or a text node";
const CHILD_SHAPE = "a child member must be a string or an array";

export function isElement(node: Node): node is Element {
	return "name" in node;
}

export function isText(node: Node): node is Text {
	return "text" in node;
}

/** Builds an element with no attributes and no children. */
export function element(
	name: string,
	attrs: Record<string, JsonValue> = {},
	children: Node[] = [],
): Element {
	return { name, attrs, children };
}

/** Builds a text node. */
export function text(value: string): Text {
	return { text: value };
}

/**
 * Whether a traversal continues past the node just visited.
 *
 * `"continue"` descends into the node's children, `"skip"` leaves them
 * unvisited and carries on with its siblings, and `"stop"` ends the traversal.
 * Returning nothing means `"continue"`.
 */
export type Walk = "continue" | "skip" | "stop";

/**
 * Where a node sits in a tree: the child indices leading to it from the root.
 *
 * Attributes are not part of a path. A consumer reporting a bad attribute
 * appends the attribute name to the element's path itself, because only the
 * consumer knows what it is rejecting.
 */
export type Path = readonly number[];

/** Renders a path as `/0/2`, and as the empty string at the root. */
export function pathToString(path: Path): string {
	return path.map((index) => `/${index}`).join("");
}

/**
 * Visits a node and its descendants in document order, parents before children.
 *
 * The traversal primitive a consumer builds its own validation or sanitization
 * on: J5ML does not define which names are legal or whether a tree is safe to
 * render, so those checks belong to the consumer that knows its output medium.
 * The path is what lets a rejection say *where*.
 *
 * ```ts
 * let found: string | undefined;
 * walk(tree, (node, path) => {
 * 	if (isElement(node) && node.name === "script") {
 * 		found = pathToString(path);
 * 		return "stop";
 * 	}
 * });
 * ```
 */
export function walk(node: Node, visit: (node: Node, path: Path) => Walk | void): void {
	walkFrom(node, [], visit);
}

function walkFrom(
	node: Node,
	trail: number[],
	visit: (node: Node, path: Path) => Walk | void,
): Walk {
	// A copy, not the live trail: a consumer that keeps a path would otherwise
	// hold an array this traversal keeps mutating underneath it.
	const outcome = visit(node, trail.slice()) ?? "continue";
	if (outcome === "stop") return "stop";
	if (outcome === "skip") return "continue";

	if (isElement(node)) {
		for (let index = 0; index < node.children.length; index += 1) {
			trail.push(index);
			const result = walkFrom(node.children[index]!, trail, visit);
			trail.pop();
			if (result === "stop") return "stop";
		}
	}

	return "continue";
}

/**
 * Parses a J5ML document.
 *
 * Accepts JSON5, which subsumes JSON: comments, trailing commas, unquoted keys,
 * and single-quoted strings all parse, and a plain JSON document parses
 * identically. There is no mode to select.
 */
export function parse(source: string): Node {
	return toNode(parseWith(JSON5.parse, source), true);
}

/**
 * Parses a J5ML document, rejecting anything JSON would reject.
 *
 * Use when a document is expected to already be canonical and JSON5 authoring
 * syntax should be an error rather than an accepted input.
 */
export function parseJson(source: string): Node {
	return toNode(parseWith(JSON.parse, source), true);
}

/**
 * Serializes a J5ML document to canonical JSON.
 *
 * Never emits JSON5 syntax. Comments and trailing commas in a parsed source do
 * not survive, because they are authoring affordances rather than document
 * content.
 */
export function stringify(node: Node): string {
	return JSON.stringify(toJson(node));
}

function parseWith(parser: (source: string) => unknown, source: string): unknown {
	try {
		return parser(source);
	} catch (cause) {
		throw new J5mlError(`J5ML parse failed: ${(cause as Error).message}`);
	}
}

function isPlainObject(value: unknown): value is Record<string, JsonValue> {
	return typeof value === "object" && value !== null && !Array.isArray(value);
}

function toNode(value: unknown, isDocument = false): Node {
	if (typeof value === "string") return { text: value };
	if (Array.isArray(value)) return elementFromMembers(value);
	throw new J5mlError(isDocument ? DOCUMENT_SHAPE : CHILD_SHAPE);
}

function elementFromMembers(members: unknown[]): Element {
	const [name, ...rest] = members;
	if (typeof name !== "string" || name.length === 0) {
		throw new J5mlError(NAME_REQUIRED);
	}

	const attrs: Record<string, JsonValue> = {};
	const children: Node[] = [];

	// The second member is attributes only when it is an object; a child is
	// always a string or an array, so the two can never be confused.
	let index = 0;
	if (rest.length > 0 && isPlainObject(rest[0])) {
		Object.assign(attrs, rest[0]);
		index = 1;
	}

	for (; index < rest.length; index += 1) {
		children.push(toNode(rest[index]));
	}

	return { name, attrs, children };
}

function toJson(node: Node): JsonValue {
	if (isText(node)) return node.text;
	if (node.name.length === 0) throw new J5mlError(NAME_REQUIRED);

	const members: JsonValue[] = [node.name];

	const names = Object.keys(node.attrs);
	if (names.length > 0) {
		members.push(canonical(node.attrs) as JsonValue);
	}

	for (const child of node.children) {
		members.push(toJson(child));
	}

	return members;
}

/**
 * Orders keys by code point rather than by JavaScript's default comparison,
 * which orders by UTF-16 code unit and disagrees with UTF-8 byte order for
 * anything above U+FFFF. Implementations that sort by bytes would otherwise
 * emit different documents for the same tree.
 */
function compareByCodePoint(a: string, b: string): number {
	const left = Array.from(a);
	const right = Array.from(b);

	for (let index = 0; index < Math.min(left.length, right.length); index += 1) {
		const difference = left[index]!.codePointAt(0)! - right[index]!.codePointAt(0)!;
		if (difference !== 0) return difference;
	}

	return left.length - right.length;
}

/**
 * Orders object keys so serialization is deterministic across implementations.
 * Applies inside attribute values too, not only at the attribute map itself.
 */
function canonical(value: JsonValue): JsonValue {
	if (Array.isArray(value)) return value.map(canonical);
	if (isPlainObject(value)) {
		const ordered: Record<string, JsonValue> = {};
		const entries = Object.entries(value).sort(([a], [b]) => compareByCodePoint(a, b));
		for (const [key, entry] of entries) {
			ordered[key] = canonical(entry);
		}
		return ordered;
	}
	return value;
}
