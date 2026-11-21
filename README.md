# Lyra Studio

Lyra Studio is a local-first Windows desktop workspace for importing provider/plugin packages, discovering supported widget and recipe catalogs, editing widget settings, and previewing supported components locally.

## Product boundary

- No WordPress installation, server, website login, or Lyra Studio Bridge is required to open the app or inspect local files.
- The application does not contact a website during startup or normal editing.
- Provider/plugin packages are imported from local ZIP archives or source folders. Standalone Provider Manifest v1 JSON files remain supported.
- A package can expose its catalog through lyra-studio.provider.json. See docs/provider-package-v1.md.
- Package source files are treated as inert data. Lyra Studio does not execute PHP, JavaScript, binaries, or other provider code.
- Preview templates are static HTML/CSS supplied by the provider manifest. They are rendered locally and are not claimed to be the real PHP/Elementor frontend output.
- Future site publishing/export can be a separate, explicit feature. It is not required for the desktop workspace.

## Install on Windows

Use the single LyraStudio-Setup.exe installer from the GitHub Actions artifact.

- The app is published as a self-contained win-x64 application. Users do not need to download or install .NET 8 separately.
- The setup contains the Microsoft WebView2 Evergreen Standalone installer and installs it silently only when the runtime is missing.
- The setup creates Start Menu shortcuts and optionally a desktop shortcut.
- The packaged application is intended for 64-bit Windows.

Developers building from source need the .NET 8 SDK. That is a development prerequisite only, not an end-user installation requirement.

## Local workflow

1. Import a plugin/provider ZIP, a source folder, or a standalone provider manifest.
2. Lyra Studio performs a bounded, read-only scan and reports the package file inventory.
3. When a compatible provider manifest is present, Studio loads its widget and recipe descriptors.
4. Select a widget. The Inspector generates controls from the provider schema.
5. Edit scalar settings, groups, conditional fields, and repeater items. The nested JSON can be copied from the Inspector.
6. The preview updates locally when a supported widget is selected. Use Desktop, Tablet, and Mobile to change the preview viewport.
7. Packages without a provider manifest are still browsable as a generic inventory. A provider-specific adapter is required for semantic source discovery.

## Provider package convention

Providers can include lyra-studio.provider.json in their ZIP or source folder. It follows the current Provider Manifest v1 schema. The manifest declares stable provider identity, widgets, control schemas, optional local widget previews, and recipe descriptors.

A generic source scan can classify source files, assets, styles and likely templates/content files. It cannot safely infer every provider's control schemas or reproduce arbitrary plugin rendering. Automatic source discovery belongs in provider-specific adapters.

A plugin archive is not necessarily a site-content export. User-created pages and templates stored in an external site's database may need to be exported separately.

## Build and test from source

    dotnet restore
    dotnet test tests/LyraStudio.Tests/LyraStudio.Tests.csproj --configuration Release
    dotnet publish src/LyraStudio/LyraStudio.csproj --configuration Release -r win-x64 --self-contained true

The CI workflow tests the app, publishes the self-contained Windows build, downloads the offline WebView2 installer, and creates the one-step Windows setup package.

## Current limitations

- Widget and recipe semantic discovery currently requires a compatible provider manifest. Native source-language adapters are the next planned milestone.
- Recipe descriptors are metadata; full page/template/section/widget tree parsing and editing are not implemented yet.
- A provider must supply a static local preview template for each widget whose appearance should be shown. The generic settings editor remains usable without one.
- The local preview is a provider-authored representation, not the real Elementor/PHP frontend.
- Saved local projects, undo/redo, media management, visual page editing, and round-trip export remain on the roadmap.
