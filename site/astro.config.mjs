import { defineConfig } from "astro/config";
import { specHeadings } from "./src/lib/spec-headings.mjs";

export default defineConfig({
	site: "https://j5ml.dev",
	markdown: {
		shikiConfig: { theme: "css-variables" },
		rehypePlugins: [specHeadings],
	},
});
