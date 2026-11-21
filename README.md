# Lyra Studio

Lyra Studio is a local-first Windows desktop workspace for importing plugin/provider packages, discovering widgets and ready-made recipes, and previewing them locally. **The first development priority is visual preview; a complete visual editor, project management, and export come later.**

## Product boundary

- No WordPress installation, server, website login, database, or Lyra Studio Bridge is required to import a package or generate a local preview.
- The application does not contact a website during startup or normal editing.
- Plugin/provider packages are imported from local ZIP archives or source folders. Standalone Provider Manifest v1 JSON files remain supported.
- A package can expose its catalog through lyra-studio.provider.json. See docs/provider-package-v1.md.
- Built-in source adapters can also read supported plugin source patterns. The first adapter reads Elementor-compatible PHP widget definitions and Lyra Page Builder recipes.
- Package source files are treated as inert data. Lyra Studio does not execute PHP, JavaScript, binaries, or other provider code.
- Local previews are generated from safe, provider-aware renderers. They are not claimed to be the real PHP/Elementor frontend output.

## First source adapter: Lyra Core / Elementor-compatible PHP

The preview-first adapter can:

- Read a static widget registry when a plugin provides one, and scan the plugin's widget source folders for additional compatible widget classes.
- Discover a variable number of widgets; the catalog is not hardcoded to the five widgets in Lyra Core 0.1.2.
- Read static get_name(), get_title(), and register_controls() declarations.
- Extract supported Elementor control types, defaults, options, simple conditions, responsive flags, and repeater sub-fields.
- Read PHP recipe arrays that use Lyra Page Builder container() and widget() calls.
- Generate local previews for the five known Lyra Core widget types and the three recipes shipped in Lyra Core 0.1.2.
- Load package-local CSS, fonts, image assets, and a safe SVG symbol sprite into the preview document.

Other Elementor widgets discovered by the adapter still appear in the catalog and Inspector. When there is no matching local visual renderer, Studio shows a structural settings preview and explains that a provider-specific renderer is not available. It does not execute a widget's PHP render() method or its JavaScript content_template().

## Local workflow

1. Import a plugin/provider ZIP, a source folder, or a standalone provider manifest.
2. Lyra Studio performs a bounded, read-only scan. ZIPs are inspected without extracting their contents.
3. Studio loads a compatible manifest or invokes a registered static source adapter.
4. Browse the discovered widgets and recipes.
5. Select a widget or recipe to see its local preview. Use Desktop, Tablet, and Mobile to change the preview viewport.
6. Edit supported widget control values in the Inspector; changes update the local preview when its renderer supports those settings.

A generic source scan can classify files, but it cannot infer every framework's semantic model. For other plugin ecosystems, an adapter must translate their native declarations into the provider-neutral Studio contracts.

## Recipes and saved site content

A plugin package may include ready-made recipe/template files, as Lyra Core does. Pages and templates saved in an external site's database are different content: they may need to be exported separately as JSON, a kit, or another supported content package. Importing a plugin ZIP alone is not represented as importing every page stored by a website.

## Install on Windows

Use the single LyraStudio-Setup.exe installer from the GitHub Actions artifact when an artifact is available.

- The app is published as a self-contained win-x64 application. Users do not need to download or install .NET 8 separately.
- The setup contains the Microsoft WebView2 Evergreen Standalone installer and installs it silently only when the runtime is missing.
- The setup creates Start Menu shortcuts and optionally a desktop shortcut.
- The packaged application is intended for 64-bit Windows.

Developers building from source need the .NET 8 SDK. That is a development prerequisite only, not an end-user installation requirement.

## Build and test from source

    dotnet restore
    dotnet test tests/LyraStudio.Tests/LyraStudio.Tests.csproj --configuration Release
    dotnet publish src/LyraStudio/LyraStudio.csproj --configuration Release -r win-x64 --self-contained true

The Windows CI workflow builds/tests the app, publishes the self-contained Windows build, downloads the offline WebView2 installer, and compiles the Inno Setup package. Uploading the artifact can be temporarily blocked when the GitHub Actions storage quota is full.

## Current limitations

- The first adapter parses a safe, static subset of Elementor-compatible PHP and Lyra Page Builder recipe expressions. Dynamic PHP expressions are not evaluated.
- Native visual preview has renderer mappings for the five known Lyra Core widget IDs. Other discovered widgets get an honest structural preview until a provider-specific renderer is available.
- The current 30-test CI suite uses a synthetic two-widget plugin fixture; automated end-to-end regression against the exact Lyra Core 0.1.2 ZIP and all three shipped recipes is still being added.
- CSS and assets are imported from the package under strict limits. Remote resources are not loaded.
- Responsive overrides can be discovered, but not every provider-specific responsive behavior is reproduced yet.
- Full neutral page editing, persistent local projects, undo/redo, media management, and round-trip export are later milestones.
- A separate clean Windows machine installation/run test is still required for final release QA.
