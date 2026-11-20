# Lyra Studio

Windows desktop workspace for discovering, configuring, and previewing Elementor widgets and page recipes from Lyra Core and future providers.

## Development principles

- Standalone Windows application built with .NET 8 WPF.
- Provider-driven catalogs and schemas; no hardcoded Lyra widget catalog.
- Lyra Studio does not modify Lyra Core or its development roadmap.
- Preview is rendered by the real WordPress + Elementor frontend, never a local imitation.
- Live previews use a separate optional WordPress Bridge and unique temporary draft pages. Existing published pages and Lyra Core Recipe Builder operations are not used.

## Run

Requirements: Windows, .NET 8 SDK, and the WebView2 Runtime.

```powershell
dotnet restore
dotnet run --project src/LyraStudio
```

## Current workflow

1. Install the separate **Lyra Studio Preview Bridge** WordPress plugin on the target site. Download the `LyraStudio-preview-bridge` artifact from the GitHub Actions run and install the ZIP through WordPress → Plugins → Add New → Upload Plugin.
2. In your WordPress user profile, create a dedicated, revocable Application Password. Do not use your main account password.
3. In Lyra Studio → **Connect WordPress**, enter the site URL, WordPress username, and Application Password. The desktop app protects the saved password with Windows DPAPI for the current Windows user. Authenticated REST requests are sent only over HTTPS.
4. Import a provider Manifest. Its widget IDs must match the Elementor widget types registered on the connected site.
5. Select a registered widget and choose **Create Elementor Preview**. Lyra Studio creates a new temporary WordPress draft through the Bridge and opens the returned WordPress + Elementor preview URL in WebView2.
6. Editing settings updates that same temporary draft. **Close Preview** requests deletion of the exact session. If the app cannot confirm deletion, the server-side TTL cleanup is the fallback.

The Application Password authenticates REST requests only. It does not sign WebView2 into WordPress. The embedded browser may ask you to log in normally with the same WordPress user to view the private draft preview.

## Widget and Recipe editing

1. Choose **Import Manifest** and select a provider JSON manifest.
2. Browse discovered widgets and recipes.
3. Select a widget to generate its Inspector from the provider's control schema.
4. Edit scalar settings, groups, conditional fields, and repeater items (add/remove/reorder).
5. Copy the resulting nested settings JSON.

Manifest import and WordPress capability discovery are read-only. The desktop preview bridge is the only current operation that writes WordPress data, and it creates a new draft page with a unique session marker. It never accepts an existing post ID or publishes a page. The Bridge does not yet translate Lyra Core PHP Recipe files into complete Elementor documents; Recipe Studio is a later milestone.

## Tests

Run automated tests on Windows:

```powershell
dotnet test tests/LyraStudio.Tests/LyraStudio.Tests.csproj --configuration Release
```

CI run 44756153679 completed the Windows build, all 34 automated tests, win-x64 publish, bridge ZIP packaging, upload of both artifacts, and PHP syntax validation.

## Security and limitations

- Use HTTPS and a dedicated, revocable WordPress Application Password.
- The Application Password is stored encrypted with DPAPI CurrentUser; it is not stored as plaintext in the connection profile.
- WordPress REST calls require an authenticated user with `edit_pages`.
- The Bridge creates only temporary draft Pages, ties each to an owner and UUID, limits active sessions, and expires stale drafts. Deletion is restricted to the exact draft/session/owner match.
- If cleanup cannot be confirmed, the desktop app reports it and the Bridge's server-side expiry cleanup remains the fallback.
- Live end-to-end compatibility still needs testing on the target WordPress + Elementor installation. The bridge PHP syntax check and desktop CI do not prove that a particular site's Elementor version or theme renders every widget correctly.

See [docs/architecture.md](docs/architecture.md), [docs/preview-integration-research.md](docs/preview-integration-research.md), [docs/preview-bridge-api-v1.md](docs/preview-bridge-api-v1.md), [ROADMAP.md](ROADMAP.md), and [docs/provider-manifest-v1.md](docs/provider-manifest-v1.md).
