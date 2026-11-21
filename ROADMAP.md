# Lyra Studio Roadmap

This roadmap is the source of truth for release scope. Percentages are recalculated from committed implementation and passing CI evidence. The product direction is local-first, Windows desktop, and provider-neutral.

## Non-negotiable product rules

- Core application workflows run on the user's Windows computer.
- Do not require WordPress, PHP, MySQL, Docker, a remote website, or a Lyra Studio WordPress Bridge.
- Do not modify Lyra Core as part of Lyra Studio work.
- Do not hardcode the widget, page, template, recipe, asset, or provider catalog.
- Import plugin/provider source packages and content exports from local files.
- Use provider adapters for framework-specific source discovery; keep the Studio core independent of any one provider.
- Preserve unknown source nodes and original metadata instead of silently dropping or guessing them.
- Never execute arbitrary imported provider code during scanning, preview, or editing.
- Do not require end users to install .NET separately; distribute a self-contained Windows app.

## Release roadmap

### v0.1.0 — Desktop foundation · 100%
- [x] WPF desktop shell and navigation
- [x] Provider contracts and base widget/recipe models
- [x] Windows CI build and automated test foundation

### v0.2.0 — Dynamic catalog and Inspector · 100%
- [x] Versioned provider-neutral JSON manifest
- [x] Local manifest import and validation
- [x] Dynamic Inspector generated from provider control schemas
- [x] Conditional controls, nested groups, repeaters and min/max validation
- [x] Nested settings JSON and clipboard export
- [x] Automated tests for manifest, controls, repeaters, and serialization

### v0.3.0 — Local preview and self-contained install · 100% · Complete
- [x] Remove WordPress connection and remote preview flow from desktop UI and source
- [x] Define static local HTML/CSS preview template fields in provider manifest
- [x] Add safe placeholder substitution and HTML encoding
- [x] Reject scripts, frames, remote URLs, event handlers and CSS imports in preview templates
- [x] Add local Desktop/Tablet/Mobile viewport controls
- [x] Configure self-contained single-file win-x64 publish
- [x] Windows CI build passes with 0 warnings and 0 errors
- [x] Automated test suite passes: 19/19
- [x] Publish self-contained win-x64 desktop application
- [x] Bundle the signed Microsoft WebView2 x64 Evergreen Standalone installer
- [x] Compile and upload the single-file Inno Setup package

### v0.4.0 — Provider package import · 0% · In progress
- [ ] Define Provider Package v1 convention using the existing provider-neutral manifest
- [ ] Inspect local ZIP packages without extracting or executing their contents
- [ ] Inspect unpacked source folders while skipping linked/reparse-point entries
- [ ] Validate path traversal, absolute paths, duplicate paths, entry counts, nesting depth and size limits
- [ ] Detect a provider manifest inside a package and load widget/recipe descriptors
- [ ] Keep legacy standalone JSON manifest import compatible
- [ ] Categorize package files for the local Package Explorer
- [ ] Add automated tests for manifest-in-ZIP, generic inventory, nested folder manifests and unsafe paths
- [ ] Verify Windows build, test suite and installer in CI

### v0.5.0 — Provider adapters and source discovery · 0%
- [ ] Define a versioned provider adapter SDK and capability negotiation
- [ ] Add adapter registration/discovery without hardcoding provider-specific logic in the Studio UI
- [ ] Build a sample adapter and a provider conformance test suite
- [ ] Add source discovery for widget definitions, controls, recipes, templates and assets
- [ ] Produce explicit supported/partial/unsupported diagnostics
- [ ] Keep provider source parsing static/read-only; do not execute arbitrary imported code

### v0.6.0 — Neutral document and recipe model · 0%
- [ ] Define provider-neutral Page, Template, Section/Container, Component/Widget and Asset nodes
- [ ] Preserve original source IDs, format/version, unknown nodes and unparsed payloads
- [ ] Parse supported recipe/page/template exports into a structured document tree
- [ ] Represent responsive overrides, global design tokens and dynamic references
- [ ] Validate round-trip compatibility without silently dropping unsupported content

### v0.7.0 — Local rendering system · 0%
- [ ] Define renderer capabilities and node-level support reports
- [ ] Implement native rendering for supported neutral document nodes
- [ ] Support safe provider-supplied local rendering bundles
- [ ] Display unsupported nodes explicitly; never imply a false full-fidelity preview
- [ ] Add responsive rendering and local asset resolution

### v0.8.0 — Visual editor and local project workflow · 0%
- [ ] Build page/template tree and visual selection workflow
- [ ] Edit supported nodes through the shared Inspector
- [ ] Save and reopen named local projects
- [ ] Persist widget settings, document variants and local drafts
- [ ] Undo/redo, reset-to-default and autosave
- [ ] Media/icon selection from local assets
- [ ] Export portable project bundles and supported source formats

### v1.0.0 — Production release · 0%
- [ ] Complete Windows x64/ARM64 packaging strategy
- [ ] Compatibility diagnostics and user documentation
- [ ] Signed installer and verified upgrade/uninstall behavior
- [ ] End-to-end QA on clean Windows machines with no .NET or WebView2 preinstalled
- [ ] Security review of package intake, adapters, local rendering and project export

## Progress calculation

Fixed release weights: v0.1.0 = 10%, v0.2.0 = 20%, v0.3.0 = 25%, v0.4.0 = 8%, v0.5.0 = 10%, v0.6.0 = 9%, v0.7.0 = 8%, v0.8.0 = 7%, v1.0.0 = 3%.

Target weighted progress after v0.4.0 is implemented and verified:
- v0.1.0: 100%
- v0.2.0: 100%
- v0.3.0: 100%
- v0.4.0: 100%
- v0.5.0: 0%
- v0.6.0: 0%
- v0.7.0: 0%
- v0.8.0: 0%
- v1.0.0: 0%

Weighted total after successful v0.4.0 verification: **63%** (10% + 20% + 25% + 8%). Until package-import source changes pass Windows CI, the verified project progress remains **55%**.

## Latest verified build before package-import implementation

- PR: #1 (Draft), branch work/studio-foundation, base main (not merged)
- Source commit: 9e63b6789a138aa7efcd6a1d16377c4798e2741d
- GitHub Actions run: [#62](https://github.com/AliasgharFahmidekar/lyra-studio/actions/runs/44761597912) — success
- Windows build: success
- Automated test job: success (the v0.3 test suite at that commit)
- Self-contained Windows publish: success
- Offline WebView2 installer download and signature validation: success
- Inno Setup compilation and artifact upload: success
- Installer artifact: LyraStudio-Setup-win-x64 (artifact ID 14641367168, 360,598,360 bytes)

**Important:** the v0.4 package-import source changes must be re-verified by a new CI run before this roadmap's 63% total is treated as complete. The clean-machine installation/run test has not yet been performed.

## Release QA still open

The installer has been compiled and uploaded by CI. A separate clean-machine installation/run test has not yet been performed; it remains release QA, not a claimed pass.

## Progress reporting protocol

For each development update:
1. Mark only committed work complete.
2. Mark build/test/package verification separately from source implementation.
3. Recalculate the active release and weighted project progress.
4. Record the commit, PR, CI run, installer artifact, and limitations.
5. Do not count packaging as verified before Windows CI passes.
