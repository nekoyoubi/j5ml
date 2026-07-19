#!/usr/bin/env node

import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";

const version = process.argv[2];
if (!version || !/^\d+\.\d+\.\d+$/.test(version)) {
	console.error("Usage: node scripts/bump-version.mjs <version>");
	console.error("Example: node scripts/bump-version.mjs 0.2.0");
	process.exit(1);
}

const root = resolve(import.meta.dirname, "..");

const tsPkgPath = resolve(root, "ts/package.json");
const tsPkg = JSON.parse(readFileSync(tsPkgPath, "utf8"));
tsPkg.version = version;
writeFileSync(tsPkgPath, JSON.stringify(tsPkg, null, "\t") + "\n");
console.log(`  ts/package.json -> ${version}`);

const cargoPath = resolve(root, "rust/Cargo.toml");
const cargo = readFileSync(cargoPath, "utf8").replace(/^(version\s*=\s*)"[^"]*"/m, `$1"${version}"`);
writeFileSync(cargoPath, cargo);
console.log(`  rust/Cargo.toml -> ${version}`);

console.log(`\n2 files updated to ${version}`);
console.log("Run 'cargo check --manifest-path rust/Cargo.toml' to refresh Cargo.lock.");
console.log(`Add a '## v${version}' section to CHANGELOG.md before releasing.`);
