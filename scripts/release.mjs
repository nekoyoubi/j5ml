#!/usr/bin/env node

import { readFileSync } from "node:fs";
import { execSync, spawnSync } from "node:child_process";
import { resolve } from "node:path";

import { implementations } from "./implementations.mjs";

const root = resolve(import.meta.dirname, "..");
const dryRun = process.argv.includes("--dry-run");

const run = (cmd) => execSync(cmd, { cwd: root, encoding: "utf8" }).trim();
const die = (msg) => {
	console.error(msg);
	process.exit(1);
};

const versions = implementations.map((implementation) => ({
	id: implementation.id,
	manifest: implementation.manifest,
	version: implementation.readVersion(),
}));

const [first, ...rest] = versions;
const disagreeing = rest.filter((entry) => entry.version !== first.version);
if (disagreeing.length > 0) {
	const listed = versions
		.map((entry) => `  ${entry.manifest} is ${entry.version ?? "unreadable"}`)
		.join("\n");
	die(`The implementations disagree on the version:\n${listed}\nRun 'pnpm version:bump <version>' to sync them.`);
}

const version = first.version;
if (!version) die(`Could not read a version from ${first.manifest}.`);

const tag = `v${version}`;

const branch = run("git branch --show-current");
if (branch !== "main") die(`You're on '${branch}', not 'main'. Switch to main before releasing.`);

if (run("git status --porcelain")) die("Working tree is dirty. Commit or stash first.");

execSync("git fetch origin main", { cwd: root, stdio: "inherit" });
const behind = run("git rev-list HEAD..origin/main --count");
if (behind !== "0") die(`Local main is ${behind} commit(s) behind origin. Pull first.`);
const ahead = run("git rev-list origin/main..HEAD --count");
if (ahead !== "0") die(`Local main is ${ahead} commit(s) ahead of origin. Push first.`);

if (run("git tag --list " + tag)) die(`Tag ${tag} already exists.`);

const changelog = readFileSync(resolve(root, "CHANGELOG.md"), "utf8");
const header = `## ${tag}`;
const headerIndex = changelog.indexOf(`\n${header}\n`);
if (headerIndex === -1) die(`No '${header}' section in CHANGELOG.md. Add one before releasing.`);

const afterHeader = changelog.slice(headerIndex + header.length + 2);
const nextHeader = afterHeader.search(/^## /m);
const body = (nextHeader === -1 ? afterHeader : afterHeader.slice(0, nextHeader)).trim();
if (!body) die(`The '${header}' section in CHANGELOG.md is empty.`);

console.log(`Releasing ${tag}`);
for (const entry of versions) console.log(`  ${entry.id.padEnd(10)} ${entry.version}`);
console.log(`  notes: ${body.split("\n").length} lines from CHANGELOG.md`);

console.log("\nRunning test suites...");
const shell = (cmd, opts = {}) => spawnSync(cmd, { cwd: root, stdio: "inherit", shell: true, ...opts });

for (const implementation of implementations) {
	if (shell(implementation.test).status !== 0) die(`The ${implementation.id} suite failed.`);
}

if (dryRun) {
	console.log(`\nDry run: every gate passed. ${tag} is ready to release.`);
	process.exit(0);
}

execSync(`git tag -a ${tag} -m ${tag}`, { cwd: root, stdio: "inherit" });
execSync(`git push origin ${tag}`, { cwd: root, stdio: "inherit" });

shell(`gh release create ${tag} --title ${tag} --notes-file -`, {
	input: body,
	stdio: ["pipe", "inherit", "inherit"],
});

console.log(`\nRelease ${tag} created. Publish workflows should fire momentarily.`);
console.log(`View at: https://github.com/nekoyoubi/j5ml/releases/tag/${tag}`);
