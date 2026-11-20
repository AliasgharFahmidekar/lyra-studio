# Lyra Studio

Windows desktop workspace for inspecting, configuring, and previewing widgets and page recipes provided by Lyra Core and future Lyra extensions.

## Development status

The application is provider-driven and intentionally independent from the Lyra Core plugin source. It does not modify the plugin, assume a fixed widget list, or ship fabricated widget previews.

## Technology

- .NET 8 WPF
- WebView2 for the real-render preview surface
- HTTP/WordPress REST connectivity isolated behind services
- Versioned JSON provider manifests
- Schema-driven inspector models for typed controls, groups, conditions, and structured values

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
5. Adjust values and copy the resulting settings JSON.

Manifest import is read-only. It does not execute manifest content or modify WordPress. The current inspector supports common scalar controls, groups, conditions, and structured JSON values. The full visual repeater editor and real Elementor preview are scheduled for later releases.

## Architecture principles

1. **Plugin-agnostic desktop:** all plugin-specific integration lives behind a provider/adapter boundary.
2. **No plugin changes in this phase:** Lyra Studio work does not alter the Lyra Core development path.
3. **No hardcoded widget catalog:** widgets, controls, recipes, templates, and their versions come from provider data.
4. **Real previews only:** the preview surface must display HTML rendered by the actual WordPress + Elementor runtime. A local imitation is never presented as a real preview.
5. **Safe editing:** importing manifests is read-only. Nothing is published or written back unless a future, separately designed operation is explicitly confirmed.
6. **Future-proof controls:** inspector controls are represented as typed metadata, including nested groups, repeaters, responsive values, conditions, and defaults.

Run automated tests on Windows with `dotnet test tests/LyraStudio.Tests/LyraStudio.Tests.csproj --configuration Release`.\n\nSee [docs/architecture.md](docs/architecture.md), [ROADMAP.md](ROADMAP.md), and [docs/provider-manifest-v1.md](docs/provider-manifest-v1.md).
