# Lyra Studio architecture

## Product boundary

Lyra Studio is a local-first Windows desktop application. Core workflows must work without WordPress installed, running, reachable, or signed in. The application must not start a local WordPress/PHP stack, database, Docker container, or background website service.

Lyra Studio does not modify Lyra Core. Provider packages and manifests are imported as local files. The Studio core remains provider-neutral: no hardcoded widget, page, template, recipe, source-language, or CMS catalog.

**Development priority:** deliver useful local previews of plugin widgets and ready-made recipes first. Full visual page editing, persistent projects, and export come after preview coverage.

## Architecture layers

1. **Studio Core** — desktop shell, workspaces, navigation, Inspector, local settings and shared commands.
2. **Package Intake** — safe ZIP/folder inspection, package inventory, manifest validation, bounded file reads and import diagnostics.
3. **Provider Adapter Registry** — optional source adapters that recognize a package shape and translate source declarations into provider-neutral widget/recipe metadata.
4. **Preview Model** — widget descriptors, recipe descriptors, settings schemas, package assets and renderer identifiers; preserves source IDs and native formats.
5. **Renderer System** — native Lyra renderers, structural fallback renderers, and optional provider-supplied local renderers. Renderer capabilities are explicit.
6. **Later Editor/Project Workflow** — neutral page tree, visual selection, save/reopen, undo/redo, local drafts and export. This is intentionally after the preview-first milestone.

## Preview-first import flow

1. The user imports a plugin/provider ZIP, a source folder, a Provider Manifest, or (in a later content-package milestone) an exported page/template package.
2. Package Intake validates path and size limits, refuses unsafe ZIP paths and links, and creates a read-only file inventory.
3. If lyra-studio.provider.json or provider-manifest.json exists, the Provider Manifest v1 loader reads it.
4. Otherwise, the adapter registry selects a compatible static source adapter. The first built-in adapter reads Elementor-compatible PHP widget patterns and Lyra Page Builder recipe files.
5. The adapter discovers the widget count dynamically, extracts supported controls/repeater schemas, and parses static recipe structures.
6. Package-local CSS, fonts, image assets and safe SVG symbols are loaded under strict limits. External resource URLs are not fetched.
7. The preview renderer produces local HTML/CSS for supported widgets and recipes. Widgets without a matching visual renderer get a structural settings preview with an explicit limitation notice.
8. Studio displays widget/recipe catalogs and previews. It does not require a full editable neutral page tree for this first preview release.

No PHP, WordPress, Elementor, JavaScript, or executable code from the imported plugin is executed. The PHP adapter reads a safe, bounded subset of static declarations only.

## First adapter: Elementor-compatible PHP and Lyra Page Builder recipes

The first adapter:
- Reads a static class-to-file widget registry when present and also scans additional widget PHP files under the package's widgets folders.
- Does not assume a fixed number of widgets.
- Reads static get_name(), get_title(), and register_controls() declarations.
- Maps supported Elementor control types, defaults, options, conditions, responsive flags, repeaters and repeater child fields into the Studio Inspector schema.
- Reads Lyra Page Builder container() / widget() recipe arrays from PHP or JSON.
- Uses safe local renderers for the known Lyra Core widget IDs. Other discovered Elementor widgets remain available with a structural fallback until a renderer is added.
- Reads the provider's CSS tokens/components/fonts, resolves local font URLs to data URIs, and embeds bounded package images and safe SVG symbol sprites.

The adapter is a best-effort static reader, not a PHP interpreter. Dynamic expressions, hooks, site/database values, WooCommerce values, Elementor Pro dynamic tags, arbitrary render() code, and JavaScript content_template() are not executed. Unsupported expressions are reported or represented as unknown rather than guessed.

## Provider contracts

The existing Provider Manifest v1 remains the provider-neutral interchange format:
- stable providerId, providerName, and provider version;
- widget identifiers, labels, control schemas and optional local preview templates;
- recipe descriptors with native format and formatVersion.

The package convention is documented in docs/provider-package-v1.md. Legacy standalone JSON manifests continue to work. Source adapters are an alternate route to the same internal descriptors; the UI does not contain framework-specific parsing logic.

## Preview fidelity levels

- **Native preview:** a built-in local renderer understands the widget/recipe and its settings. Lyra Core 0.1.2's known widgets are the first native target.
- **Structural preview:** the widget and its controls are discovered, but no faithful visual renderer exists. Studio shows an honest settings-based structural representation.
- **Unsupported source construct:** the item remains visible in diagnostics/inventory; Studio does not invent a visual result or discard its source.

A native preview is still a local Studio representation, not a claim that the PHP/Elementor production frontend is running or pixel-identical.

## Later neutral document model

After the first preview release, the model expands to include:
- Page, Template, Section/Container, Component/Widget and Asset nodes;
- source IDs, original format/version and preserved unknown payloads;
- responsive overrides, global design tokens, dynamic references and asset graphs;
- round-trip validation and explicit unsupported-node reporting.

The model must not force every provider into Elementor terminology or flatten every document into a widget list.

## Desktop packaging

- WPF targets net8.0-windows and is published win-x64, self-contained and single-file.
- End users do not install the .NET Desktop Runtime separately.
- The app uses WebView2 for local HTML rendering. The one-step Inno Setup installer includes the Microsoft WebView2 Evergreen Standalone installer and silently installs the runtime only when it is missing.
- The setup package is compiled by Windows CI. The CI runner's .NET SDK is a build-time requirement, not a customer requirement.

## Security and isolation

- ZIPs are inspected without extracting their contents to arbitrary destinations.
- Absolute paths, path traversal, duplicate paths, symbolic links and reparse-point entries are rejected.
- Folder scanning does not follow links/reparse points.
- File count, depth, per-file, total-package and manifest limits apply.
- Imported PHP, JavaScript, macros and executables are inert data and are never run.
- Static PHP expression parsing is bounded and does not evaluate arbitrary functions or code.
- Local CSS resources are resolved only inside the imported package and embedded within byte limits; remote URLs and CSS imports are rejected.
- HTML dynamic values are encoded. SVG symbol content is checked for executable/remote markup before inclusion.
- The rendered preview document applies a restrictive Content Security Policy.

## Current implementation

Implemented before the preview-first adapter: desktop shell, local manifest import, safe ZIP/folder package inventory, provider manifest detection, schema-driven widget Inspector, conditional controls, repeaters, nested settings JSON, static provider HTML/CSS preview, responsive viewport controls, and self-contained Windows packaging.

The current active preview milestone adds: a PHP source adapter, dynamic widget discovery, static control/repeater parsing, Lyra recipe parsing, package CSS/font/icon/image loading, and native/structural local previews.

Later work: adapters for other ecosystems, a richer neutral document model, broader renderer coverage, full visual page editing, persistent local projects, undo/redo, and provider-specific round-trip export.
