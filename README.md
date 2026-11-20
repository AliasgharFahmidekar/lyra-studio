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
- Optional, separate WordPress Preview Bridge for temporary Elementor draft sessions

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

Manifest import and WordPress capability discovery are read-only. They do not execute manifest content or modify WordPress. The inspector supports common scalar controls, nested groups, conditional visibility, and schema-generated repeater item fields with collection limits.

## Real Elementor preview integration

The optional **Lyra Studio Preview Bridge** is a separate WordPress plugin under [integrations/lyra-studio-preview-bridge](integrations/lyra-studio-preview-bridge). It does not modify or depend on Lyra Core. It provides authenticated REST routes to create, update, and delete only uniquely marked temporary WordPress draft pages rendered by Elementor.

The bridge package is not automatically installed on a WordPress site. Download the `LyraStudio-preview-bridge` artifact from GitHub Actions and install it separately when ready to test real previews. Use a dedicated, revocable WordPress Application Password over HTTPS for REST requests. The Application Password authenticates API calls only; WebView2 still needs a normal WordPress browser session to display a private draft preview.

**The desktop app's Bridge client and full live-preview workflow are not yet implemented.** The current release only provides the standalone bridge package and REST capability diagnostics. No existing published page or Lyra Core recipe is used for preview.

## Tests

Run automated tests on Windows:

```powershell
dotnet test tests/LyraStudio.Tests/LyraStudio.Tests.csproj --configuration Release
```

The Windows CI also runs PHP syntax validation for the optional bridge.

## Architecture principles

1. **Plugin-agnostic desktop:** all plugin-specific integration lives behind a provider/adapter boundary.
2. **No Lyra Core changes:** the desktop app and optional preview bridge are maintained separately from the Lyra Core development path.
3. **No hardcoded widget catalog:** widgets, controls, recipes, templates, and their versions come from provider data.
4. **Real previews only:** the preview surface must display HTML rendered by the actual WordPress + Elementor runtime. A local imitation is never presented as a real preview.
5. **Safe editing:** manifest imports and capability discovery are read-only. Preview writes are restricted to a dedicated temporary draft with an exact session marker.
6. **Future-proof controls:** inspector controls are represented as typed metadata, including nested groups, repeaters, responsive values, conditions, and defaults.

See [docs/architecture.md](docs/architecture.md), [docs/preview-integration-research.md](docs/preview-integration-research.md), [docs/preview-bridge-api-v1.md](docs/preview-bridge-api-v1.md), [ROADMAP.md](ROADMAP.md), and [docs/provider-manifest-v1.md](docs/provider-manifest-v1.md).
