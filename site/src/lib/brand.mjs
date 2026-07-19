import { derive, resolveIconMark, composeIconThemed, iconClass } from "@xtyle/core";
import { getAlgorithm } from "@xtyle/core/algorithms";

import { SIGMAR_SUBSET_WOFF2_BASE64 } from "./sigmar-subset.mjs";

export const algorithm = getAlgorithm("xtyle-default");

export const VERSION = "0.1.0";
export const LICENSE = "MIT OR Apache-2.0";

export const THEME_KNOBS = { vibrancy: 1, radiusScale: 1.5 };

export const THEME_CONSTRAINTS = {
	"--accent": "#47a5ff",
	"--bg-0": "#24282a",
};

export const register = derive(algorithm, {
	knobs: THEME_KNOBS,
	constraints: THEME_CONSTRAINTS,
});

const LOGO_BASE =
	"j5ml--letter-j-x-20-y-6-s85-c5-o2cb--letter-5-x10-y-4-s130-cf-o2cb---d5p8s1t50--f0-Sigmar";

/** The masthead pins its own palette, so the mark colors the same wherever it lands. */
export const MASTHEAD_NAME = `${LOGO_BASE}--ps-skittles`;

/**
 * Marks of the projects this site leans on, verbatim from their own mastheads so
 * the badge a reader recognizes elsewhere is the badge they see here. The site is
 * built with xtyle;
 * xript is the other direction, a consumer of this format. Both typeset in
 * Sigmar, which the page already loads.
 */
export const XTYLE_NAME =
	"xtyle--letter-x-x-2-y-6-s120-c9-o2cb--column-x22-s145-ko--letter-t-x16-s105-cf-o2cb---d9p8s1t20--f0-Sigmar--ps-skittles";

export const XRIPT_NAME =
	"xript--letter-x-x-12-y-4-s110-c8-o2cb--column-x16-s150-ko--letter-r-x18-y-4-s115-cf-o2cb---f0-Sigmar--ps-skittles";

const hex = (token) => register[token].replace("#", "");

/**
 * The favicon pins both letters to literal hex rather than to tokens. A token
 * override stays theme-reactive and emits `var(--accent)`, which resolves for an
 * inline mark but not in a favicon: that renders as an isolated document with no
 * cascade to read. The values come from the register so they cannot drift.
 */
export const FAVICON_NAME =
	`${LOGO_BASE}--pc5-${hex("--accent")}--pcf-ffffff--pcb-${hex("--bg-0")}`;

/**
 * Reproduces what `@xtyle/astro`'s `Icon` emits for a generated name, including
 * the `xtyle-icon` class its sizing rules key off and the `data-root data-icon`
 * wrapper. Only the register differs: `Icon` derives one through
 * `resolveAlgorithm` (a filesystem read the published package cannot satisfy);
 * this uses the same algorithm imported as JS.
 */
export function bakeLogo({ name = MASTHEAD_NAME, size = "md", px } = {}) {
	const parsed = resolveIconMark(name);
	if (!parsed) return null;

	// No `scheme` is passed: each name pins its own palette, so a host-supplied
	// one would only override what the name already states.
	let svg = composeIconThemed(parsed.composition, {
		register,
		part: "icon",
		className: iconClass({ size }),
	});
	if (!svg) return null;

	if (px != null) svg = svg.replace('width="1em" height="1em"', `width="${px}" height="${px}"`);

	return `<span data-root data-icon>${svg}</span>`;
}

/**
 * The same mark as a standalone document, for the favicon.
 *
 * A favicon renders isolated from the page, so it reaches neither the document's
 * webfonts nor its CSS custom properties. The composed mark typesets in Sigmar,
 * which therefore has to travel inside the file or silently fall back to a
 * default face.
 */
export function bakeFavicon(size = 32) {
	const svg = (bakeLogo({ name: FAVICON_NAME, px: size }) || "").replace(/^<span[^>]*>|<\/span>$/g, "");
	if (!svg) return null;

	const face = `<style>@font-face{font-family:"Sigmar";font-style:normal;font-weight:400;src:url(data:font/woff2;base64,${SIGMAR_SUBSET_WOFF2_BASE64}) format("woff2");}</style>`;

	return svg.replace("<defs>", `${face}<defs>`);
}
