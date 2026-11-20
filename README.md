# Lyra Studio

Windows desktop workspace for inspecting, configuring, and previewing widgets and page recipes provided by Lyra Core and future Lyra extensions.

## Development status

The repository currently contains the **desktop foundation** only. The application is intentionally independent from the Lyra Core plugin source. It does not modify the plugin, assume a fixed widget list, or ship fabricated widget previews.

## Technology

- .NET 8 WPF
- WebView2 for the eventual browser-based live preview surface
- HTTP/WordPress REST connectivity isolated behind services
- Schema-driven inspector models so future widgets and controls can be discovered dynamically

## Run

Requirements: Windows, .NET 8 SDK, and the WebView2 Runtime.

```powershell
dotnet restore
dotnet run --project src/LyraStudio
```

## Architecture principles

1. **Plugin-agnostic desktop:** all plugin-specific integration lives behind a provider/adapter boundary.
2. **No plugin changes in this phase:** connect and inspect only capabilities that already exist; any future plugin-side contract must be proposed separately.
3. **No hardcoded widget catalog:** widgets, controls, recipes, and their versions must be discovered from the connected provider.
4. **Real previews only:** the preview surface must display HTML rendered by the actual WordPress + Elementor runtime. A local imitation is not presented as a real preview.
5. **Safe editing:** preview sessions are isolated/draft by default. Nothing is published or written back unless the user explicitly exports/applies changes.
6. **Future-proof controls:** inspector controls are represented as typed schemas, including nested groups, repeaters, responsive values, conditions, and defaults.

See [docs/architecture.md](docs/architecture.md) for the initial design.
