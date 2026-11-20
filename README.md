# Lyra Studio

Windows desktop workspace for inspecting, configuring, and previewing widgets and page recipes provided by Lyra Core and future Lyra extensions.

## Development status

The application is provider-driven and intentionally independent from the Lyra Core plugin source. It does not modify the plugin, assume a fixed widget list, or ship fabricated widget previews.

## Technology

- .NET 8 WPF
- WebView2 for the real-render preview surface
- HTTP/WordPress REST connectivity isolated behind services
- Versioned JSON provider manifests
- Schema-driven inspector models for typed controls, groups, conditions, repeaters, and structured values

## Run

Requirements: Windows, .NET 8 SDK, and the WebView2 Runtime.

```powershell
dotnet restore
dotnet run --project src/LyraStudio
```

## Current workflow

1. Open Lyra Studio on Windows.
2. Choose **Import Manifest** and select a provider JSON manifest.
3. Browse the discovered widgets or recipes.
4. Select a widget to generate its settings inspector from the manifest schema.
5. Edit repeater items with add, remove, and reorder controls.
6. Adjust values and copy the resulting nested settings JSON.
7. Use **Connect WordPress** to inspect the site's public REST API namespaces and declared routes.

Manifest import and WordPress capability discovery are read-only. They do not execute manifest content or modify WordPress. The inspector supports common scalar controls, nested groups, conditional visibility, and schema-generated repeater item fields with collection limits. The connection report does not infer permissions or claim that an Elementor preview endpoint exists. Real Elementor preview is scheduled for a later release.

## Tests

Run automated tests on Windows:

```powershell
dotnet test tests/LyraStudio.Tests/LyraStudio.Tests.csproj --configuration Release
```

## Architecture principles

1. **Plugin-agnostic desktop:** all plugin-specific integration lives behind a provider/adapter boundary.
2. **No plugin changes in this phase:** Lyra Studio work does not alter the Lyra Core development path.
3. **No hardcoded widget catalog:** widgets, controls, recipes, templates, and their versions come from provider data.
4. **Real previews only:** the preview surface must display HTML rendered by the actual WordPress + Elementor runtime. A local imitation is never presented as a real preview.
5. **Safe editing:** importing manifests and reading REST discovery information are read-only. Nothing is published or written back unless a future, separately designed operation is explicitly confirmed.
6. **Future-proof controls:** inspector controls are represented as typed metadata, including nested groups, repeaters, responsive values, conditions, and defaults.

See [docs/architecture.md](docs/architecture.md), [docs/preview-integration-research.md](docs/preview-integration-research.md), [ROADMAP.md](ROADMAP.md), and [docs/provider-manifest-v1.md](docs/provider-manifest-v1.md).
