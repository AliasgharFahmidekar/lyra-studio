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
- [ ] End-to-end QA on clean Windows machines with no .NET or WebView2 preinstalled (still required; CI has built the installer but has not executed it on a separate clean customer machine)

## Progress calculation

Fixed release weights: v0.1.0 = 10%, v0.2.0 = 20%, v0.3.0 = 25%, v0.4.0 = 20%, v0.5.0 = 15%, v1.0.0 = 10%.

Verified progress after Windows CI run #59:
- v0.1.0: 100%
- v0.2.0: 100%
- v0.3.0: 100%
- v0.4.0: 0%
- v0.5.0: 0%
- v1.0.0: 0%

Weighted total: **55%** (10% + 20% + 25%). Remaining work is the Recipe Studio, local project persistence/export, and final clean-machine release QA.

## Latest verified build
- PR: #1 (Draft), branch `work/studio-foundation`
- Source commit used for the installer: `e675b9ca7b45b07909d5af7d2fa84e1a4cce0100`
- GitHub Actions run: [#59](https://github.com/AliasgharFahmidekar/lyra-studio/actions/runs/44760793190) — success
- Tests: 19 passed, 0 failed
- Build: 0 warnings, 0 errors
- Installer artifact: `LyraStudio-Setup-win-x64` (artifact ID `14641113346`)
- Artifact SHA-256: `5e683e9af2bfadb4c99b8a35275541688db088b429a2c87cb409523ed651cd29`
- Artifact size in GitHub Actions: 360,598,489 bytes

The installer has been compiled and uploaded. A separate clean-machine installation/run test has not yet been performed; it remains release QA, not a claimed pass.

## Progress reporting protocol

For each development update:
1. Mark only committed work complete.
2. Mark build/test/package verification separately from source implementation.
3. Recalculate the active release and weighted project progress.
4. Record the commit, PR, CI run, installer artifact, and limitations.
5. Do not count packaging as verified before Windows CI passes.
