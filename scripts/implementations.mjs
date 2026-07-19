import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = (rel) => readFileSync(resolve(root, rel), "utf8");
const write = (rel, text) => writeFileSync(resolve(root, rel), text);

/**
 * Every implementation that carries a version and is asserted against the corpus.
 *
 * The release scripts read this rather than each naming the implementations
 * themselves. A version that only one of them knows about is the failure this
 * prevents: `version:bump` would move the two it had been told about, the release
 * gate would compare only those two and pass, and the tag would go out with one
 * registry a version behind the others. Adding a language is an entry here.
 */
export const implementations = [
	{
		id: "rust",
		manifest: "rust/Cargo.toml",
		test: "cargo test --manifest-path rust/Cargo.toml",
		readVersion: () => read("rust/Cargo.toml").match(/^version\s*=\s*"([^"]+)"/m)?.[1],
		writeVersion: (version) =>
			write("rust/Cargo.toml", read("rust/Cargo.toml").replace(/^(version\s*=\s*)"[^"]*"/m, `$1"${version}"`)),
	},
	{
		id: "typescript",
		manifest: "ts/package.json",
		test: "pnpm -C ts test",
		readVersion: () => JSON.parse(read("ts/package.json")).version,
		writeVersion: (version) => {
			const pkg = JSON.parse(read("ts/package.json"));
			pkg.version = version;
			write("ts/package.json", JSON.stringify(pkg, null, "\t") + "\n");
		},
	},
	{
		id: "csharp",
		manifest: "csharp/src/J5ml/J5ml.csproj",
		test: "dotnet test csharp/tests/J5ml.Tests/J5ml.Tests.csproj -c Release",
		readVersion: () => read("csharp/src/J5ml/J5ml.csproj").match(/<Version>([^<]+)<\/Version>/)?.[1],
		writeVersion: (version) =>
			write(
				"csharp/src/J5ml/J5ml.csproj",
				read("csharp/src/J5ml/J5ml.csproj").replace(/<Version>[^<]*<\/Version>/, `<Version>${version}</Version>`),
			),
	},
];
