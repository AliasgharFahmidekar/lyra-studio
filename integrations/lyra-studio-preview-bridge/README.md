# Lyra Studio Preview Bridge

An **optional, independent WordPress plugin** that provides Lyra Studio with an authenticated temporary-draft preview transport for Elementor.

This integration does not edit, extend, or require a code change in Lyra Core. It is a separate WordPress component. Install it only on sites where real Elementor previews are required.

## Requirements

- WordPress 6.0+
- PHP 7.4+
- Elementor active
- An authenticated WordPress user with the `edit_pages` capability
- HTTPS for remote REST requests and WordPress Application Password authentication

## Install

1. Zip the `lyra-studio-preview-bridge` folder.
2. Upload it in WordPress under Plugins → Add New → Upload Plugin.
3. Activate **Lyra Studio Preview Bridge**.
4. Generate a WordPress Application Password for the account Lyra Studio will use. Do not use the account's main password.

## REST API

Namespace: `lyra-studio/v1`

All endpoints require WordPress REST authentication and `edit_pages`. The app should use HTTPS and a dedicated, revocable Application Password.

| Method | Route | Purpose |
|---|---|---|
| GET | `/capabilities` | Reports WordPress/Elementor versions and registered Elementor widget names |
| POST | `/previews` | Creates a new temporary Elementor draft and renders its preview URL |
| POST/PUT/PATCH | `/previews/{session_id}` | Updates only that session's temporary draft |
| DELETE | `/previews/{session_id}` | Deletes only that session's temporary draft |

### Create preview

Request JSON:

```json
{
  "title": "Lyra Studio widget preview",
  "ttl_seconds": 1800,
  "settings": {},
  "elements": [
    {
      "id": "a1b2c3d",
      "elType": "section",
      "settings": {},
      "elements": [
        {
          "id": "d4e5f6a",
          "elType": "column",
          "settings": { "_column_size": 100 },
          "elements": [
            {
              "id": "b7c8d9e",
              "elType": "widget",
              "widgetType": "lyra-section-heading",
              "settings": {},
              "elements": []
            }
          ]
        }
      ]
    }
  ]
}
```

The endpoint always creates a WordPress **draft page**. The caller cannot request a published/private status. The bridge uses Elementor's document manager to create and save the document, then returns the preview URL.

### Update and delete

The session UUID is associated with the exact draft post, owner user ID, and expiry timestamp in post meta. Update requires the same authenticated owner and requires the post to remain a draft. Delete permanently removes only the exact draft whose stored session UUID matches the request.

If a draft is changed to another post status, the bridge refuses to update or delete it. Expired sessions are cleaned up by a five-minute WP-Cron task, but only while the post remains a draft and the session marker still matches.

## Safety behavior

- No endpoint accepts a target post ID supplied by the caller.
- No endpoint can publish or overwrite an existing page.
- Drafts are created with a unique session UUID and owner marker.
- Maximum five active sessions per user.
- Default expiry is 30 minutes; the maximum accepted TTL is 2 hours.
- Request elements are limited to 2 MB.
- Cleanup checks post type, draft status, and exact session UUID before permanent deletion.
- Deactivation unschedules cleanup but deliberately leaves remaining drafts untouched.
- The bridge never purges Elementor's global CSS cache, because that could affect unrelated live pages.

## Preview browser login

The REST Application Password is for API calls only. It cannot sign into wp-admin or the preview page. When WebView2 opens the returned preview URL, the user must sign into WordPress in that embedded browser with the matching WordPress account. WordPress then checks the user's permission to view the draft.

## Compatibility notes

- The bridge uses Elementor's document manager API, not Lyra Core's recipe builder.
- The actual output is rendered by WordPress and Elementor on the frontend.
- Widget registration, CSS, scripts, theme context, and third-party addon dependencies remain the responsibility of the WordPress site.
- This first bridge release accepts an Elementor document tree from Lyra Studio; it does not convert Lyra Core PHP Recipe files by itself.
