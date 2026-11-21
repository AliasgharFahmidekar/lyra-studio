# Lyra Studio architecture

## Product boundary

Lyra Studio is a local-first Windows desktop application. Core workflows must work without WordPress installed, running, reachable, or signed in. The application must not start a local WordPress/PHP stack, database, Docker container, or background website service.

Lyra Studio does not modify Lyra Core. Provider packages and manifests are imported as local files. The Studio core must remain provider-neutral: no hardcoded widget, page, template, recipe, source-language, or CMS catalog.

## Architecture layers

1. **Studio Core** — desktop shell, workspaces, local storage, navigation, Inspector, project lifecycle and common commands.
2. **Package Intake** — safe ZIP/folder inspection, package inventory, identity/version metadata, manifest validation and import diagnostics.
3. **Provider Adapters** — optional adapters that understand a specific source ecosystem and translate its native declarations into the Studio contract. They are plugins to the Studio, not special cases scattered through the UI.
4. **Neutral Document Model** — pages, templates, sections/containers, widgets/components, settings, responsive values, design tokens, dynamic references, asset references and preserved unknown nodes.
5. **Renderer System** — native Studio renderers and optional provider-supplied local renderers, selected by capability. Unsupported content remains visible in the tree and is never silently deleted.
6. **Editor and Project Workflow** — visual tree, Inspector, preview, save/reopen, versioned project files, undo/redo, validation and export.

## Package intake and local data flow

1. The user imports a plugin/provider ZIP, a source folder, a provider manifest, or (in a later content-package milestone) an exported page/template package.
2. Package Intake validates paths and size limits, refuses unsafe archive paths and link entries, and creates a read-only file inventory.
3. If lyra-studio.provider.json or provider-manifest.json is present, the existing Provider Manifest v1 loader validates and reads it.
4. If there is no compatible manifest, Studio reports a generic source inventory and the missing adapter/metadata requirement. It does not invent widget schemas by guessing.
5. A registered Provider Adapter can later inspect the inert package and expose native widgets, templates, recipes and capabilities through provider-neutral descriptors.
6. The document model preserves native IDs, original format/version, unknown nodes and source metadata.
7. A renderer reports exactly which nodes it can display. Preview output is never presented as the provider's real frontend unless that renderer actually produces it.

No runtime request is sent to WordPress. There is no remote preview session, REST credential, Bridge, draft page, or server-side cleanup process.

## Provider contract

The current interchange format is Provider Manifest v1:
- stable providerId, providerName, and provider version;
- widget identifiers, labels, control schemas and optional local preview templates;
- recipe descriptors that retain their native format and formatVersion.

The package convention is documented in docs/provider-package-v1.md. Legacy standalone JSON manifests continue to work.

A generic package scan can inventory files but cannot infer every source framework's semantic model. Provider adapters are responsible for language/framework-specific discovery and must expose capabilities and limitations. They must not execute arbitrary imported PHP, JavaScript, shell scripts or binaries as part of package inspection.

## Document model principles

The future document model is intentionally broader than any one plugin builder:
- distinguish widget/component definitions from instances in a page;
- distinguish reusable recipes/blueprints from complete pages and reusable templates;
- preserve section/container hierarchy, order, responsive overrides and global design tokens;
- preserve unknown native nodes and original data losslessly where possible;
- keep asset references and dynamic data references explicit;
- carry source provider, format and version metadata for round-trip export.

The Studio must not flatten every document into a widget list or force all providers into Elementor terminology.

## Renderer capabilities

Three capability levels are planned:
- **Native renderer:** maps supported neutral document nodes to Studio rendering primitives.
- **Provider renderer:** a provider supplies a safe local representation and declares which nodes/settings it supports.
- **Unsupported:** the node stays visible and inspectable with a clear explanation; it is not silently omitted.

The current widget preview is a static provider-authored HTML/CSS representation. It is not the WordPress/Elementor PHP frontend and is not claimed to be pixel-identical.

## Desktop packaging

- WPF targets net8.0-windows and is published win-x64, self-contained and single-file.
- End users do not install the .NET Desktop Runtime separately.
- The app uses WebView2 for local HTML rendering. The one-step Inno Setup installer includes the Microsoft WebView2 Evergreen Standalone installer and silently installs the runtime only when it is missing.
- The setup package is built by Windows CI. The CI runner's .NET SDK is a build-time requirement, not a customer requirement.

## Security and isolation

- ZIPs are inspected without extracting their contents to arbitrary destinations.
- Absolute paths, path traversal, duplicate paths, symbolic links and reparse-point entries are rejected.
- Folder scanning does not follow links/reparse points.
- File count, depth, per-file, total-package and manifest limits apply.
- Imported source code is inert. No arbitrary PHP, JavaScript, macros or executables are run.
- Local preview templates are validated for executable and remote content, and dynamic text values are HTML-encoded.
- The generated preview document applies a restrictive Content Security Policy.

## Current implementation

Implemented: desktop shell, local manifest import, safe ZIP/folder package inventory, provider manifest detection inside packages, schema-driven widget Inspector, conditional controls, repeaters, nested settings JSON, local HTML/CSS widget preview, responsive viewport controls, self-contained Windows packaging.

Not yet implemented: source-language discovery adapters, provider SDK/sample adapter, parsed recipe/page/template document trees, complete design-system and asset graph, native rendering for arbitrary provider content, persistent local projects, undo/redo, visual page editing and provider-specific round-trip export.
