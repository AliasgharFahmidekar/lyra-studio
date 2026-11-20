# Lyra Studio Roadmap

This roadmap is the source of truth for release scope and progress. Update the checkboxes and percentages whenever implementation work is completed or reclassified.

## Product constraints

- Standalone Windows desktop application.
- Do not modify Lyra Core or its development roadmap as part of Lyra Studio work.
- Do not hardcode the current widget, page, template, or recipe catalog.
- Future providers must be able to add widgets and controls through a versioned, documented contract.
- A preview may be called "real" only when it is rendered by the actual WordPress + Elementor runtime.
- No operation may publish or overwrite a live page without an explicit, separately designed confirmation flow.

## Release roadmap

### v0.1.0 — Desktop foundation · 75%

- [x] WPF application shell and navigation
- [x] WordPress URL profile persistence and read-only REST connectivity check
- [x] Provider interfaces and base widget/recipe/preview models
- [ ] Verified clean Windows CI build and publish artifact

### v0.2.0 — Dynamic catalog and inspector · 67% · Current

- [x] Versioned, provider-neutral JSON manifest contract
- [x] Manifest import and validation with duplicate and version checks
- [x] Dynamic inspector generated from control metadata
- [x] Widget and recipe catalog views populated from the imported manifest
- [ ] Rich repeater editor with add/remove/reorder and per-item nested fields
- [ ] Automated tests for manifest compatibility, unknown controls, and conditional visibility

### v0.3.0 — Real Elementor preview · 0%

- [ ] Verify available preview/render capabilities without changing Lyra Core
- [ ] Implement a preview-provider adapter for a compatible existing endpoint or supported local bridge
- [ ] Create isolated temporary preview sessions
- [ ] Embed real WordPress + Elementor output in WebView2
- [ ] Desktop/tablet/mobile viewport controls and preview cleanup

### v0.4.0 — Recipe Studio · 0%

- [ ] Discover and inspect recipe metadata and format versions
- [ ] Parse page/section/widget structure through provider-specific serializers
- [ ] Visual recipe tree with selection and safe draft editing
- [ ] Import/export supported recipe formats
- [ ] Recipe validation and compatibility reports

### v0.5.0 — Authoring workflow · 0%

- [ ] Persist local drafts and named widget setting variants
- [ ] Undo/redo and reset-to-default
- [ ] Media/icon selection through provider capabilities
- [ ] Responsive settings editor and per-device comparison
- [ ] Export/copy settings and recipe bundles

### v1.0.0 — Production release · 0%

- [ ] Complete Windows build/test pipeline and signed/reproducible release packaging
- [ ] First-run setup, connection management, diagnostics, and safe credential storage
- [ ] Compatibility checks for supported WordPress, Elementor, and provider versions
- [ ] User documentation, sample provider contract, and release notes
- [ ] End-to-end validation against a real WordPress + Elementor installation

## Progress calculation

Release weights are fixed for the current roadmap: v0.1.0 = 10%, v0.2.0 = 20%, v0.3.0 = 25%, v0.4.0 = 20%, v0.5.0 = 15%, v1.0.0 = 10%. The project percentage is the sum of each release weight multiplied by that release's completion percentage. Unstarted releases count as 0%. A task is not marked complete until its implementation is committed; tasks requiring runtime/CI evidence remain open until that evidence exists.

Current weighted project progress: **21%** (v0.1.0: 75% × 10%, v0.2.0: 67% × 20%, all later releases: 0%).

## Progress update protocol

For every development update:
1. Mark only completed, committed work as complete.
2. Recalculate the current release percentage and weighted project percentage.
3. Record the commit/PR and any unverified build or runtime limitations.
4. Keep this roadmap synchronized with the release notes and the user-facing progress report.
