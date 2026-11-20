# Lyra Studio Preview Bridge API v1

This is the transport contract implemented by the optional WordPress plugin under `integrations/lyra-studio-preview-bridge`. It is independent of Lyra Core.

## Base path and authentication

Base path: `/wp-json/lyra-studio/v1`

All endpoints require an authenticated WordPress user with `edit_pages`. Remote desktop requests must use HTTPS and a dedicated WordPress Application Password. The desktop client encrypts the Application Password with Windows DPAPI `CurrentUser` scope and refuses to send Basic Authentication credentials over HTTP.

The REST credential does not authenticate the embedded browser. WebView2 uses its own WordPress web session for the returned preview URL.

## Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/capabilities` | Reports bridge, WordPress, and Elementor versions and registered widget identifiers |
| POST | `/previews` | Creates a temporary draft preview document |
| POST/PUT/PATCH | `/previews/{session_id}` | Replaces the Elementor data on the same owned draft |
| DELETE | `/previews/{session_id}` | Deletes the exact owned draft |

## Create/update payload

```json
{
  "title": "Lyra Studio Preview",
  "ttl_seconds": 1800,
  "settings": {},
  "elements": []
}
```

The desktop app generates a minimal Elementor Section → Column → Widget document for the selected registered widget and puts the inspector settings on that widget element. `elements` must be an array of Elementor document elements. Combined JSON payload size is limited to 2 MB. The bridge defaults TTL to 30 minutes and caps it at two hours.

## Successful create/update response

```json
{
  "session_id": "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx",
  "post_id": 123,
  "status": "draft",
  "preview_url": "https://example.test/?p=123&preview=true",
  "expires_at": "2026-11-20T15:00:00+00:00",
  "elementor": "x.y.z"
}
```

These values illustrate response shape only. The desktop client uses the exact session ID, preview URL, status, and expiry returned by the server, then verifies that the preview URL matches the connected WordPress origin.

## Client lifecycle

1. Call `GET /capabilities` and verify `elementor_ready=true`.
2. Verify that the selected Manifest widget ID appears in the returned Elementor widget type list.
3. Generate an Elementor element tree from the selected widget and current settings.
4. Call `POST /previews`; save the returned session ID and expiry in the current in-memory Studio session.
5. Navigate WebView2 to the returned `preview_url`. Sign in through the embedded browser if WordPress requests it.
6. On Inspector changes, debounce and send a replacement `elements` tree to the same session endpoint.
7. On Close Preview or application exit, call `DELETE` for the same session.
8. If deletion cannot be confirmed, tell the user and leave expiry cleanup as the fallback.

The client never posts to an arbitrary post ID or deletes by title, slug, or guessed identifier.

## Session rules

- The server creates a new Page; the client never selects a post ID.
- The page is always created with `draft` status.
- The session ID and owner ID are stored as private post meta.
- Update requires the exact session ID, matching current user, `edit_post` permission and a post that is still a draft.
- Delete requires the same exact marker and owner and refuses non-draft posts.
- A maximum of five active sessions per user is enforced.
- Cleanup runs through a one-shot WP-Cron event every five minutes while marked draft sessions remain. It deletes only exact draft pages whose session ID and owner still match.
- Deactivating the bridge cancels cleanup and leaves remaining drafts untouched.
