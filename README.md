# Lyra Studio

Lyra Studio is a Windows desktop workspace for local widget editing, recipes, and responsive previews. Its normal workflow is entirely on the computer.

## Product boundary

- No WordPress installation, server, website login, or Lyra Studio Bridge is required to open the app, import a provider manifest, edit widget settings, or use a local preview template.
- The application does not contact WordPress during startup or normal editing.
- Provider catalogs are imported from local JSON files.
- Preview templates are static HTML/CSS supplied by the provider manifest. Lyra Studio renders them locally in its embedded preview surface. This is a provider-supplied local representation, not a claim that PHP/Elementor frontend rendering is running locally.
- Future site publishing/export can be a separate, explicit feature. It is not required for the desktop workspace.

## Install on Windows

Use the single LyraStudio-Setup.exe installer from the GitHub Actions artifact.

- The app is published as a self-contained win-x64 application. Users do not need to download or install .NET 8 separately.
- The setup contains the Microsoft WebView2 Evergreen Standalone installer and installs it silently only when the runtime is missing.
- The setup creates Start Menu shortcuts and optionally a desktop shortcut.
- The packaged application is intended for 64-bit Windows.

Developers building from source need the .NET 8 SDK. That is a development prerequisite only, not an end-user installation requirement.

## Local workflow

1. Open Lyra Studio. No server connection is shown or required.
2. Select Import Manifest and choose a provider JSON manifest on the computer.
3. Select a widget. Lyra Studio generates the controls from the provider schema.
4. Edit scalar settings, groups, conditional fields, and repeater items. The nested JSON can be copied from the Inspector.
5. The preview updates locally when the settings change. Use Desktop, Tablet, and Mobile to change the preview viewport.
6. Widgets without a local preview template remain editable; the preview explains that the provider has not supplied a renderer for that widget yet.

## Local preview template contract

A widget may include a preview object with static html and css fields. HTML placeholders use {{setting.path}}; values are HTML-encoded before insertion. {{name}} and {{id}} expose the widget name and ID.

The preview template cannot execute scripts or load remote content. The manifest loader rejects script/frame elements, event handlers, JavaScript URLs, remote URLs, CSS imports, and oversized templates. The WebView2 document also uses a restrictive Content Security Policy.

See docs/provider-manifest-v1.md for the contract, docs/architecture.md for boundaries, and ROADMAP.md for progress.

## Build and test from source

    dotnet restore
    dotnet test tests/LyraStudio.Tests/LyraStudio.Tests.csproj --configuration Release
    dotnet publish src/LyraStudio/LyraStudio.csproj --configuration Release -r win-x64 --self-contained true

The CI workflow tests the app, publishes the self-contained Windows build, downloads the offline WebView2 installer, and creates the one-step Windows setup package.

## Current limitations

- A provider must supply a static local preview template for each widget whose appearance should be shown. The generic settings editor remains usable without one.
- The local preview shows the provider-authored template and is not the real Elementor/PHP frontend output.
- Full recipe structure editing, saved local projects, undo/redo, media management, and export workflows remain on the roadmap.
