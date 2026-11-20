# Lyra Studio architecture

## Product boundary

Lyra Studio is a local-first Windows desktop application. Its core workflows must work without WordPress installed, running, reachable, or signed in. The application must not start a local WordPress/PHP stack, database, Docker container, or background website service.

Lyra Studio does not modify Lyra Core. Provider packages or manifests are imported as local files, and provider catalogs remain data-driven rather than compiled into a hardcoded widget list.

## Local data flow

1. A user imports a provider manifest from disk.
2. The manifest describes widget IDs, labels, control schemas, recipes, and optional static preview templates.
3. The dynamic Inspector builds its controls from the manifest.
4. Settings are held locally in the desktop process and serialized as nested JSON.
5. The local preview renderer combines the provider static HTML/CSS template with HTML-encoded setting values and displays it in the embedded browser.
6. Local project persistence and import/export are added in later milestones.

No runtime request is sent to WordPress. There is no remote preview session, REST credential, Bridge, draft page, or server-side cleanup process.

## Provider contract

- IWidgetCatalogProvider and IRecipeCatalogProvider expose local catalogs.
- The versioned JSON manifest is the current interchange format.
- A widget can optionally provide preview.html and preview.css.
- Placeholders such as {{title}} and {{layout.subtitle}} are resolved against current settings.
- Placeholder values are HTML-encoded.
- Preview templates are static only. The manifest loader rejects executable or remote markup; the generated document applies a restrictive Content Security Policy.
- A widget without a local template can still be edited, but Lyra Studio displays a clear missing-template message instead of pretending to render it.

The local preview is intentionally called a provider preview. It is not the WordPress/Elementor PHP frontend and must not be described as pixel-identical to that frontend.

## Desktop packaging

- WPF targets net8.0-windows and is published win-x64, self-contained and single-file.
- End users do not install the .NET Desktop Runtime separately.
- The app uses WebView2 for local HTML rendering. The one-step Inno Setup installer includes the Microsoft WebView2 Evergreen Standalone installer and silently installs the runtime only when it is missing.
- The setup package is built and tested by Windows CI. The CI runner having the .NET SDK is a build-time requirement, not a customer requirement.

## Security and isolation

- Preview HTML is generated from a provider-authored local template.
- Dynamic text values are HTML-encoded.
- JavaScript, iframes, event attributes, JavaScript URLs, remote URLs and CSS imports are rejected in preview templates.
- The rendered document sets a restrictive Content Security Policy.
- The local preview does not load arbitrary network resources.

## Current implementation

The desktop shell, local manifest import, schema-driven Inspector, conditional controls, repeater editor, nested settings JSON, local HTML/CSS preview rendering, responsive viewport controls, and self-contained packaging workflow are implemented. Recipe structure editing, persistent local projects, undo/redo, media handling, and provider-specific real-world widget templates remain future work.
