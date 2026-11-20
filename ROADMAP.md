# Lyra Studio Roadmap

This roadmap is the source of truth for release scope and progress. Percentages are recalculated from completed, committed work and verified CI/runtime evidence.

## Product constraints

- Standalone Windows desktop application.
- Do not modify Lyra Core or its development roadmap as part of Lyra Studio work.
- Do not hardcode the current widget, page, template, or recipe catalog.
- Future providers must be able to add widgets and controls through a versioned, documented contract.
- A preview may be called "real" only when it is rendered by the actual WordPress + Elementor runtime.
- No operation may publish or overwrite a live page without an explicit, separately designed confirmation flow.

## Release roadmap

### v0.1.0 — Desktop foundation · 100%

- [x] WPF application shell and navigation
- [x] WordPress URL profile persistence and read-only REST connectivity check
- [x] Provider interfaces and base widget/recipe/preview models
- [x] Verified clean Windows CI build, win-x64 publish, and artifact upload (run 44749365180)

### v0.2.0 — Dynamic catalog and inspector · 100%

- [x] Versioned, provider-neutral JSON manifest contract
- [x] Manifest import and validation with duplicate and version checks
- [x] Dynamic inspector generated from control metadata
- [x] Widget and recipe catalog views populated from the imported manifest
- [x] Visual repeater editor with schema-generated nested fields, add/remove/reorder, and min/max enforcement
- [x] Automated tests passing in Windows CI for manifest validation, repeater operations, settings JSON serialization, and conditional visibility (14/14 passed, run 44751045277)

### v0.3.0 — Real Elementor preview · 86% · Current

- [x] Verify supplied Lyra Core 0.1.1 and Elementor preview/render capabilities without changing Core
- [x] Read-only WordPress REST discovery diagnostics (24/24 tests passed, run 44752714846)
- [x] Select and document the optional, separate WordPress Preview Bridge transport
- [x] Implement authenticated temporary-draft Bridge endpoints with owner/session-bound updates, deletion, TTL and exact-draft cleanup (PHP lint passed, run 44756153679)
- [x] Implement WPF Bridge client and DPAPI CurrentUser encryption for WordPress Application Passwords
- [x] Add widget preview create/update/close workflow and navigate WebView2 to the actual WordPress + Elementor draft preview URL
- [ ] Run end-to-end validation against a real WordPress + Elementor site and add desktop/tablet/mobile viewport controls

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

Release weights are fixed: v0.1.0 = 10%, v0.2.0 = 20%, v0.3.0 = 25%, v0.4.0 = 20%, v0.5.0 = 15%, v1.0.0 = 10%. The project percentage is the weighted sum of release completion percentages. Percentages are rounded to the nearest whole number for display.

Current weighted project progress: **51%** (v0.1.0: 100% × 10%, v0.2.0: 100% × 20%, v0.3.0: 6/7 = 85.7% × 25%, later releases: 0%; unrounded total 51.43%).

## Latest CI verification

Windows workflow run: 44756153679

- Desktop build: succeeded
- Automated tests: 34 passed, 0 failed
- win-x64 publish: succeeded
- Optional Preview Bridge package: uploaded
- PHP syntax lint: succeeded
- App artifact: `LyraStudio-win-x64`
- Bridge artifact: `LyraStudio-preview-bridge`

The bridge has passed PHP syntax lint, but its real WordPress/Elementor behavior has not yet been exercised on the target site. End-to-end validation remains open.

## Progress update protocol

For every development update:
1. Mark only completed, committed work as complete.
2. Recalculate current release and weighted project percentages.
3. Record the commit/PR and any unverified build or runtime limitations.
4. Keep this roadmap synchronized with release notes and user-facing progress reports.
5. Never count CI work as complete before its full relevant job succeeds.
