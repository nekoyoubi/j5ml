/**
 * Pins the examples in `README.md`, which is the npm package page.
 *
 * Rust gets this for free: its README is `include_str!`'d as the crate docs, so
 * every example there is a doctest. TypeScript has no equivalent, so the samples
 * would rot silently. The expected outputs are read out of the README itself
 * rather than copied here, so editing one without the other fails.
 */

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { isElement, parse, pathToString, stringify, walk } from "../src/index.js";

const readme = readFileSync(fileURLToPath(new URL("../README.md", import.meta.url)), "utf8");

/** The single-quoted literal in a `// comment` on the line following `marker`. */
function documentedOutput(marker: string): string {
	const line = readme.split("\n").find((candidate) => candidate.includes(marker));
	if (!line) throw new Error(`README no longer contains: ${marker}`);

	const match = line.match(/\/\/ '(.*)'$/);
	if (!match?.[1]) throw new Error(`no documented output on: ${line}`);

	return match[1];
}

describe("README examples", () => {
	it("parses and serializes the opening sample", () => {
		const node = parse('["div",{"class":"panel"},"Hello"]');

		expect(node).toEqual({
			name: "div",
			attrs: { class: "panel" },
			children: [{ text: "Hello" }],
		});

		expect(stringify(node)).toBe(documentedOutput("stringify(node); //"));
	});

	it("canonicalizes the hand-authored JSON5 sample", () => {
		const node = parse(`
			// authored by hand
			['Gauge', { percent: 62.5, }]
		`);

		expect(stringify(node)).toBe(documentedOutput("stringify(node); // '[\"Gauge\""));
	});

	it("finds a script node with walk, at the path the README claims", () => {
		const tree = parse('["doc",{},["script",{},"x"]]');

		let found: string | undefined;
		walk(tree, (node, path) => {
			if (isElement(node) && node.name === "script") {
				found = pathToString(path);
				return "stop";
			}
		});

		expect(readme).toContain('console.assert(found === "/0")');
		expect(found).toBe("/0");
	});
});
