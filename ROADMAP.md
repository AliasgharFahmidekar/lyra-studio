# Lyra Studio Roadmap

This roadmap is the source of truth for release scope. Progress is recalculated from committed implementation and passing Windows CI evidence. Lyra Studio is local-first, Windows desktop, and provider-neutral.

## Non-negotiable product rules

- Core application workflows run on the user's Windows computer.
- Do not require WordPress, PHP, MySQL, Docker, a remote website, or a Lyra Studio WordPress Bridge.
- Do not modify Lyra Core as part of Lyra Studio work.
- Do not hardcode widget counts, page/template/recipe catalogs, or one provider into the Studio core.
- Import plugin/provider source packages and content exports from local files.
- Use provider adapters for framework-specific source discovery; keep the Studio core independent of any one provider.
- Preserve unknown source nodes and original metadata instead of silently dropping or guessing them.
- Never execute arbitrary imported provider code during scanning, preview, or editing.
- Prioritize a useful local visual preview before building a complete visual editor or project-management workflow.
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

### v0.3.0 — Local preview foundation and self-contained install · 100% · Complete
- [x] Remove WordPress connection and remote preview flow from desktop UI and source
- [x] Define static local HTML/CSS preview template fields in provider manifest
- [x] Add safe placeholder substitution and HTML encoding
- [x] Reject executable or remote content in provider preview templates
- [x] Add local Desktop/Tablet/Mobile viewport controls
- [x] Configure self-contained single-file win-x64 publish
- [x] Windows CI build and the 19-test suite pass
- [x] Publish self-contained win-x64 desktop application
- [x] Bundle the signed Microsoft WebView2 x64 Evergreen Standalone installer
- [x] Compile the single-file Inno Setup package

### v0.4.0 — Local plugin/package intake · 100% · Complete
- [x] Define Provider Package v1 convention using the existing provider-neutral manifest
- [x] Inspect local ZIP packages without extracting or executing their contents
- [x] Inspect unpacked source folders while skipping linked/reparse-point entries
- [x] Validate path traversal, absolute paths, duplicate paths, entry counts, nesting depth and size limits
- [x] Detect a provider manifest inside a package and load widget/recipe descriptors
- [x] Keep legacy standalone JSON manifest import compatible
- [x] Categorize package files in the local Package Explorer
- [x] Add tests for manifest-in-ZIP, generic inventory, nested folder manifests and unsafe paths
- [x] Verify Windows build, tests, self-contained publish, WebView2 signature validation and Inno Setup compilation in CI

**Artifact note:** CI runs #64 and #65 compiled the installer successfully, but GitHub refused the final artifact upload because the Actions storage quota was full. The build/test/publish/installer-compile steps passed; this is an artifact-storage limitation, not a source compile failure.

### v0.5.0 — Preview-first source adapter · 80% · Active
- [x] Define the initial provider adapter contract and local adapter registry
- [x] Read Elementor-compatible widget registries and discover additional widget classes from source folders
- [x] Discover a variable number of widgets without a fixed catalog
- [x] Extract supported control definitions, labels, defaults, options, simple conditions, responsive flags and repeater children
- [x] Parse the supported static Lyra Page Builder PHP recipe expressions into container/widget structures
- [x] Add native local renderer mappings for the five known Lyra Core 0.1.2 widget IDs
- [x] Generate local recipe previews from supported parsed container/widget structures
- [x] Load package-local CSS, fonts, bounded images and safe SVG symbols with external-resource restrictions
- [x] Provide an honest structural settings preview when no native visual renderer exists
- [ ] Add fixtures/tests based on Lyra Core 0.1.2 and a second differently sized Elementor-compatible plugin
- [ ] Add exact Lyra Core 0.1.2 regression coverage for all five widgets and all three shipped recipes

### v0.6.0 — General provider SDK and broader adapters · 0%
- [ ] Version the provider adapter SDK and capability negotiation
- [ ] Define adapter discovery/registration independent of the Studio UI
- [ ] Build a sample provider and adapter conformance suite
- [ ] Add adapters for other plugin/framework structures without changing the core widget catalog
- [ ] Add diagnostics for supported, partially supported, and unsupported source constructs

### v0.7.0 — Neutral document model and preview coverage · 0%
- [ ] Define provider-neutral Page, Template, Section/Container, Component/Widget and Asset nodes
- [ ] Preserve original source IDs, format/version, unknown nodes and unparsed payloads
- [ ] Normalize responsive values, global design tokens and dynamic references
- [ ] Expand native rendering across supported neutral document nodes
- [ ] Validate preview coverage and round-trip compatibility without silently dropping unsupported content

### v0.8.0 — Visual editor and local project workflow · 0%
- [ ] Build a page/template tree and visual selection workflow
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

Fixed release weights: v0.1.0 = 10%, v0.2.0 = 20%, v0.3.0 = 25%, v0.4.0 = 8%, v0.5.0 = 15%, v0.6.0 = 10%, v0.7.0 = 6%, v0.8.0 = 3%, v1.0.0 = 3%.

Verified progress after Windows CI runs #72/#73 (source commit f22f9159a23db5c9f661f1107c9e639a123955fe):
- v0.1.0: 100%
- v0.2.0: 100%
- v0.3.0: 100%
- v0.4.0: 100%
- v0.5.0: 80% (adapter, dynamic discovery, control parsing, recipe previews, native renderer mappings and CI verification are implemented; exact-package and second-provider regression fixtures remain)
- v0.6.0: 0%
- v0.7.0: 0%
- v0.8.0: 0%
- v1.0.0: 0%

Weighted total: **75%** (10% + 20% + 25% + 8% + 12% from v0.5.0). The immediate next goal is to strengthen exact-package and cross-provider preview regression coverage. Visual page editing and project persistence intentionally remain later.

## Latest verified preview-first CI

- PR: #1 (Draft), branch work/studio-foundation, base main (not merged)
- Source commit: f22f9159a23db5c9f661f1107c9e639a123955fe
- CI push run #72: https://github.com/AliasgharFahmidekar/lyra-studio/actions/runs/44949888805 — success
- CI pull-request run #73: https://github.com/AliasgharFahmidekar/lyra-studio/actions/runs/44949895035 — success
- Windows build: success
- Automated tests: 30 passed, 0 failed
- Self-contained Windows publish: success
- Offline WebView2 download and Authenticode validation: success
- Inno Setup compilation: success
- Artifact upload: blocked by GitHub Actions storage quota; the run logs report that no new artifact could be stored. Build and installer compilation still passed because the quota-limited upload is reported separately.

The regression tests currently use a synthetic, two-widget Elementor-compatible package fixture with a Lyra widget and recipe expression. They do not yet run the exact uploaded Lyra Core 0.1.2 ZIP as a committed automated fixture. That exact-package check and broader renderer coverage are the remaining v0.5 tasks.

The separate clean-machine installation/run test remains open.

## Progress reporting protocol

For each development update:
1. Mark only committed and tested work complete.
2. Mark source implementation, automated tests, installer compilation, and artifact upload separately.
3. Recalculate the active release and weighted project progress.
4. Record the commit, PR, CI run, installer artifact status, and limitations.
5. Do not claim a package artifact was uploaded when GitHub storage quota prevented it.
