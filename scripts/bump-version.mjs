#!/usr/bin/env node

import { implementations } from "./implementations.mjs";

const version = process.argv[2];
if (!version || /^\d+\.\d+\.\d+$/.test(version) === false) {
	console.error("Usage: node scripts/bump-version.mjs <version>");
	console.error("Example: node scripts/bump-version.mjs 0.3.0");
	process.exit(1);
}

for (const implementation of implementations) {
	implementation.writeVersion(version);
	console.log(`  ${implementation.manifest} -> ${version}`);
}

console.log(`\n${implementations.length} files updated to ${version}`);
console.log("Run 'cargo check --manifest-path rust/Cargo.toml' to refresh Cargo.lock.");
console.log(`Add a '## v${version}' section to CHANGELOG.md before releasing.`);
