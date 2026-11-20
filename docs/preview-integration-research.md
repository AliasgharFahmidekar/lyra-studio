# Preview integration research — v0.3.0

## Scope and safety boundary

This research covers the supplied Lyra Core 0.1.1 archive and the public WordPress/Elementor integration surface. It does **not** modify Lyra Core, create a WordPress page, update Elementor data, or publish anything.

## What the supplied Lyra Core 0.1.1 package contains

The reviewed package includes:

- `includes/class-lyra-elementor.php` — the Elementor integration and widget manifest.
- `includes/class-lyra-base-widget.php` — the shared Elementor widget base.
- `includes/class-lyra-page-builder.php` — recipe-to-WordPress-page/template builder.
- `includes/class-lyra-settings.php` — admin actions for building recipes and assigning WooCommerce content.
- Two concrete widgets: Section Heading and Feature Grid.
- One example recipe: `includes/recipes/example-landing.php`.

The package has no Lyra-specific `register_rest_route()` endpoint, no dedicated preview endpoint, and no AJAX bridge for Lyra Studio. The widget manifest is a PHP-side manifest, not a desktop-readable JSON discovery feed.

The page builder uses WordPress post creation/update operations and stores Elementor data in post meta. It can update an existing page/template when a recipe is rebuilt. Therefore, calling the recipe builder as a preview mechanism would not be a safe read-only operation and is explicitly excluded.

## What Elementor's rendering documentation establishes

Elementor documents two widget rendering paths:

- PHP `render()` creates frontend HTML.
- JavaScript `content_template()` renders the widget preview inside Elementor's editor.

That distinction matters: rendering the PHP template requires the actual WordPress/Elementor runtime and its registered widgets, styles, scripts, and frontend context. A desktop-only recreation would be an approximation, not an Elementor preview.

Elementor's current source registers the document `save_builder` lifecycle through its AJAX action manager. The save implementation verifies that the document is built with Elementor and editable by the current user, saves the document, then returns a WordPress preview URL. This is an authenticated editor save workflow, not a generic anonymous REST preview route. It must not be called against a user's existing document without an explicit and separately designed workflow.

## Current Lyra Studio capability discovery

Lyra Studio now reads the WordPress REST index using HEAD-based REST URL discovery where the host exposes the standard `Link: rel="https://api.w.org/"` header, with a conventional `/wp-json/` fallback. It parses the index namespaces and route names to report:

- Whether the standard WordPress Pages route is declared.
- Whether an Elementor REST namespace is listed, and which Elementor routes are visible.
- Whether a Lyra/Lyra Core/Lyra Studio namespace is listed.

This report is informational only. A route being listed does not prove that the connected user is authorized to call it, that the endpoint accepts Elementor document data, or that it supports temporary preview sessions.

## Recommended preview architecture

Do not use the existing Lyra recipe builder directly for preview. The safe implementation needs an explicit temporary-document lifecycle:

1. Confirm a compatible and authenticated WordPress/Elementor integration path.
2. Create a uniquely marked temporary **draft** document owned by the connected user or a dedicated service identity.
3. Store a Studio session ID and expiry marker with that temporary document.
4. Load it through WordPress's real frontend/preview pipeline inside WebView2.
5. Apply settings only to that temporary draft, using Elementor's supported editor lifecycle where required.
6. Delete only the exact document created by that Studio session after the preview is closed or expires.
7. If cleanup cannot be verified, leave it as draft and surface a cleanup warning; never delete a document based only on a title or slug match.

This protocol requires a verified transport. Possibilities to evaluate are:

- A separate optional Lyra Studio bridge plugin, independent from Lyra Core's roadmap, with narrowly scoped authenticated REST routes for discovery, temporary draft creation/update, render URL, and cleanup.
- An authenticated local WordPress/Elementor editor bridge that uses the site's supported editor lifecycle and only a Studio-created draft document.

The first option provides a stable versioned interface and leaves Lyra Core untouched, but it is an additional optional WordPress component and needs separate approval before implementation. The second may reduce server-side integration work but is more dependent on Elementor editor internals and session handling.

## Sources

- Elementor Widget Rendering: https://developers.elementor.com/docs/widgets/widget-rendering/
- Elementor render content hook: https://developers.elementor.com/docs/hooks/render-widget-content/
- WordPress REST API discovery: https://developer.wordpress.org/rest-api/using-the-rest-api/discovery/
- WordPress REST API route and endpoint reference: https://developer.wordpress.org/rest-api/extending-the-rest-api/routes-and-endpoints/
- Elementor Documents Manager source (save lifecycle): https://github.com/elementor/elementor/blob/main/core/documents-manager.php

## Current conclusion

The plugin archive proves that Lyra widgets can be rendered by Elementor on a WordPress site. It does **not** provide the authenticated, temporary preview lifecycle needed by Lyra Studio. The REST index diagnostic is complete; live rendering remains blocked until a separate safe integration transport is implemented and verified.
