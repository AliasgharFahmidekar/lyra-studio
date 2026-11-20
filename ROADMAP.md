# Lyra Studio Roadmap

This roadmap is the source of truth for release scope. Percentages are recalculated from committed implementation and passing CI evidence. The product direction is local-first and desktop-only.

## Non-negotiable product rules

- Core application workflows run on the user's Windows computer.
- Do not require WordPress, PHP, MySQL, Docker, a remote website, or a Lyra Studio WordPress Bridge.
- Do not modify Lyra Core as part of Lyra Studio work.
- Do not hardcode the widget, page, template, or recipe catalog.
- Import provider data from local files.
- Local previews must come from provider-supplied preview templates. Do not claim they are the actual Elementor PHP frontend.
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

### v0.3.0 — Local preview and self-contained install · 86% · Current
- [x] Remove WordPress connection and remote preview flow from desktop UI and source
- [x] Define static local HTML/CSS preview template fields in provider manifest
- [x] Add safe placeholder substitution and HTML encoding
- [x] Reject scripts, frames, remote URLs, event handlers and CSS imports in preview templates
- [x] Add local Desktop/Tablet/Mobile viewport controls
- [x] Configure self-contained single-file win-x64 publish
- [ ] Windows CI validates local preview tests and complete offline installer

### v0.4.0 — Recipe Studio · 0%
- [ ] Parse and edit recipe/page/section/widget structure
- [ ] Visual recipe tree and safe local editing
- [ ] Import/export supported recipe formats
- [ ] Recipe validation and compatibility reports

### v0.5.0 — Local project workflow · 0%
- [ ] Save and reopen named local projects
- [ ] Persist widget settings variants and local drafts
- [ ] Undo/redo, reset-to-default and autosave
- [ ] Media/icon selection from local assets
- [ ] Export portable project bundles

### v1.0.0 — Production release · 0%
- [ ] Complete Windows x64/ARM64 packaging strategy
- [ ] Versioned provider SDK and sample provider
- [ ] Compatibility diagnostics and user documentation
- [ ] Signed installer and verified upgrade/uninstall behavior
- [ ] End-to-end QA on clean Windows machines with no .NET or WebView2 preinstalled

## Progress calculation

Fixed release weights: v0.1.0 = 10%, v0.2.0 = 20%, v0.3.0 = 25%, v0.4.0 = 20%, v0.5.0 = 15%, v1.0.0 = 10%.

Current implementation progress before Windows CI validation:
- v0.1.0: 100%
- v0.2.0: 100%
- v0.3.0: 6/7 = 85.7%
- later releases: 0%

Weighted total: **51%** (10% + 20% + 21.43% = 51.43%, rounded). The remaining v0.3 CI item is not counted as complete until the full Windows build, tests, and installer job succeeds.

## Progress reporting protocol

For each development update:
1. Mark only committed work complete.
2. Mark build/test/package verification separately from source implementation.
3. Recalculate the active release and weighted project progress.
4. Record the commit, PR, CI run, installer artifact, and limitations.
5. Do not count packaging as verified before Windows CI passes.
