/**
 * Guarantees the shared corpus cannot express: it records documents, not the
 * behavior of a builder API.
 */

import { describe, expect, it } from "vitest";

import { element, isElement, parse, pathToString, stringify, text, walk, type Path } from "../src/index.js";

describe("invariants", () => {
	it("refuses to serialize an empty name", () => {
		expect(() => stringify(element(""))).toThrow();
	});

	it("round-trips a built element", () => {
		const built = element("Gauge", { percent: 62.5 }, [text("ready")]);
		expect(parse(stringify(built))).toEqual(built);
	});

	it("orders attribute names regardless of insertion order", () => {
		const built = element("Block", { title: "Health", border_style: { fg: "Cyan" } });
		expect(stringify(built)).toBe('["Block",{"border_style":{"fg":"Cyan"},"title":"Health"}]');
	});

	it("orders keys nested inside an attribute value", () => {
		const built = element("Gauge", { style: { fg: "Green", bold: true } });
		expect(stringify(built)).toBe('["Gauge",{"style":{"bold":true,"fg":"Green"}}]');
	});

	it("writes an integral number without a fractional part", () => {
		expect(stringify(element("n", { v: 100.0 }))).toBe('["n",{"v":100}]');
	});
});

describe("key ordering matches UTF-8 byte order", () => {
	it("orders an astral-plane name after a BMP name", () => {
		// U+1F600 sorts after U+E000 by code point and by UTF-8 bytes, but before
		// it under JavaScript's default UTF-16 code-unit comparison.
		const built = element("x", { "\u{1F600}": 1, "": 2 });
		expect(stringify(built)).toBe('["x",{"":2,"\u{1F600}":1}]');
	});
});

describe("walk", () => {
	const label = (node: ReturnType<typeof parse>) => (isElement(node) ? node.name : node.text);

	it("visits parents before children, reporting each path", () => {
		const tree = parse('["a",{},["b",{},"one"],["c",{}]]');
		const seen: string[] = [];
		walk(tree, (node, path) => {
			seen.push(`${pathToString(path)}:${label(node)}`);
		});
		expect(seen).toEqual([":a", "/0:b", "/0/0:one", "/1:c"]);
	});

	it("skips a subtree without ending the traversal", () => {
		const tree = parse('["a",{},["b",{},"deep"],["c",{}]]');
		const seen: string[] = [];
		walk(tree, (node) => {
			if (isElement(node) && node.name === "b") return "skip";
			seen.push(label(node));
		});
		expect(seen).toEqual(["a", "c"]);
	});

	it("stops immediately on request", () => {
		const tree = parse('["a",{},["b",{},"deep"],["c",{}]]');
		let visited = 0;
		walk(tree, () => {
			visited += 1;
			return "stop";
		});
		expect(visited).toBe(1);
	});

	it("hands out a path that later mutation cannot corrupt", () => {
		const tree = parse('["a",{},["b",{}],["c",{}]]');
		const kept: Path[] = [];
		walk(tree, (_node, path) => {
			kept.push(path);
		});
		expect(kept.map(pathToString)).toEqual(["", "/0", "/1"]);
	});
});
