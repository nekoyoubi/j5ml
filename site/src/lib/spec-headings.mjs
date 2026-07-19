/**
 * Rehype plugin that reshapes the embedded spec for the page it renders inside.
 *
 * `spec/j5ml.md` is a standalone document and owns an `<h1>`, which is correct
 * when it is read on its own. Inlined into the site it sits inside the page's
 * own "Spec" section, so every heading drops two levels to nest under that
 * `<h2>` and each id gains a `spec-` prefix to clear the page's own slugs.
 *
 * The prefixed id is assigned here rather than rewritten afterwards because
 * `rehype-slug` runs later in the pipeline and leaves an existing id alone.
 */
const textOf = (node) =>
	node.type === "text" ? node.value : (node.children ?? []).map(textOf).join("");

const slug = (text) =>
	text
		.toLowerCase()
		.trim()
		.replace(/[^\w\s-]/g, "")
		.replace(/\s+/g, "-");

export function specHeadings() {
	const visit = (node) => {
		if (node.type === "element") {
			const match = /^h([1-4])$/.exec(node.tagName);
			if (match) {
				node.tagName = `h${Number(match[1]) + 2}`;
				node.properties ??= {};
				node.properties.id ??= `spec-${slug(textOf(node))}`;
			}
		}
		for (const child of node.children ?? []) visit(child);
	};
	return (tree) => visit(tree);
}
