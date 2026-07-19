import { bakeFavicon } from "../lib/brand.mjs";

export const GET = () =>
	new Response(bakeFavicon(32), {
		headers: {
			"Content-Type": "image/svg+xml",
			"Cache-Control": "public, max-age=604800",
		},
	});
