#!/usr/bin/env node

import { readFileSync } from "node:fs";
import { execSync, spawnSync } from "node:child_process";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const dryRun = process.argv.includes("--dry-run");

const run = (cmd) => execSync(cmd, { cwd: root, encoding: "utf8" }).trim();
const die = (msg) => {
	console.error(msg);
	process.exit(1);
};

const tsVersion = JSON.parse(readFileSync(resolve(root, "ts/package.json"), "utf8")).version;
const cargoVersion = readFileSync(resolve(root, "rust/Cargo.toml"), "utf8").match(/^version\s*=\s*"([^"]+)"/m)?.[1];

if (tsVersion !== cargoVersion)
	die(`Version mismatch: ts/package.json is ${tsVersion}, rust/Cargo.toml is ${cargoVersion}.\nRun 'pnpm version:bump <version>' to sync them.`);

const version = tsVersion;
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
console.log(`  rust:  ${cargoVersion}`);
console.log(`  ts:    ${tsVersion}`);
console.log(`  notes: ${body.split("\n").length} lines from CHANGELOG.md`);

console.log("\nRunning test suites...");
const rust = spawnSync("cargo", ["test", "--manifest-path", "rust/Cargo.toml"], { cwd: root, stdio: "inherit", shell: true });
if (rust.status !== 0) die("Rust tests failed.");
const ts = spawnSync("pnpm", ["-C", "ts", "test"], { cwd: root, stdio: "inherit", shell: true });
if (ts.status !== 0) die("TypeScript tests failed.");

if (dryRun) {
	console.log(`\nDry run: every gate passed. ${tag} is ready to release.`);
	process.exit(0);
}

execSync(`git tag -a ${tag} -m ${tag}`, { cwd: root, stdio: "inherit" });
execSync(`git push origin ${tag}`, { cwd: root, stdio: "inherit" });

spawnSync("gh", ["release", "create", tag, "--title", tag, "--notes-file", "-"], {
	cwd: root,
	input: body,
	stdio: ["pipe", "inherit", "inherit"],
	shell: true,
});

console.log(`\nRelease ${tag} created: https://github.com/nekoyoubi/j5ml/releases/tag/${tag}`);
console.log("\nPublish the packages:");
console.log("  cargo publish --manifest-path rust/Cargo.toml");
console.log("  pnpm -C ts build && pnpm -C ts publish --access public --no-git-checks");
