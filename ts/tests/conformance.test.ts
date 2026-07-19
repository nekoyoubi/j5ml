/**
 * Asserts this implementation against the shared corpus.
 *
 * The corpus is authoritative. A case that fails here is either a bug in this
 * implementation or a change to the format that every other implementation in
 * the repository must also make.
 *
 * The corpus's neutral tree encoding is this implementation's own `Node` shape,
 * so an expected tree is compared directly rather than being rebuilt.
 */

import { describe, expect, it } from "vitest";

import corpus from "../../spec/conformance-tests.json" with { type: "json" };
import { parse, parseJson, stringify, type Node } from "../src/index.js";

interface Case {
	name: string;
	json: string;
	tree: Node;
	roundTrips: boolean;
	serializesTo?: string;
}

interface Authoring {
	name: string;
	json5: string;
	serializesTo: string;
}

interface Invalid {
	name: string;
	json: string;
	reason: string;
}

describe("canonical cases", () => {
	it.each(corpus.cases as Case[])("$name", (testCase) => {
		const parsed = parse(testCase.json);
		expect(parsed).toEqual(testCase.tree);

		expect(parseJson(testCase.json)).toEqual(testCase.tree);

		const expected = testCase.roundTrips ? testCase.json : testCase.serializesTo;
		expect(stringify(parsed)).toBe(expected);
	});
});

describe("authoring cases", () => {
	it.each(corpus.authoring as Authoring[])("$name", (testCase) => {
		expect(stringify(parse(testCase.json5))).toBe(testCase.serializesTo);
	});
});

describe("invalid cases", () => {
	it.each(corpus.invalid as Invalid[])("$name", (testCase) => {
		expect(() => parse(testCase.json)).toThrow();
	});
});
