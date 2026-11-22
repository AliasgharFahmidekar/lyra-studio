# Provider Manifest v1

Lyra Studio imports provider manifests from local files. The contract belongs to Lyra Studio and does not require Lyra Core to change immediately. A future provider adapter can generate this document from a plugin release, local source package, or another compatible provider.

## Top-level fields

| Field | Type | Required | Meaning |
|---|---|---:|---|
| manifestVersion | integer | Yes | Contract version. Currently 1. |
| providerId | string | Yes | Stable provider identifier. |
| providerName | string | Yes | Human-readable provider name. |
| providerVersion | string | No | Provider release/version. |
| widgets | array | No | Widget descriptors. |
| recipes | array | No | Recipe descriptors. |

## Widget descriptor

A widget contains id, name, optional category, description, version, controls, and optional preview.

Each control contains a stable name, display label, string kind, optional description, required, defaultValue, options, children, conditions, constraints, and isResponsive.

Supported control kinds include text, textarea, number, toggle, select, color, url, media, icon, group, repeater, typography, dimensions, slider, and date. Unknown kinds are retained rather than dropped.

A repeater uses its children array as its per-item field schema. The Inspector supports adding, removing, reordering, and min/max item limits.

## Local preview

A widget may provide a preview object:
- html: static HTML template.
- css: optional static CSS.

Use {{setting.path}} placeholders to insert values. Dotted paths resolve nested objects; numeric segments can resolve array items. Dynamic values are HTML-encoded. Two built-in tokens are supported: {{name}} and {{id}}.

Example:

    {
      "id": "example.notice",
      "name": "Notice",
      "controls": [
        { "name": "title", "label": "Title", "kind": "text", "defaultValue": "Hello" },
        { "name": "subtitle", "label": "Subtitle", "kind": "text", "defaultValue": "Local preview" }
      ],
      "preview": {
        "html": "<article class='notice'><h2>{{title}}</h2><p>{{subtitle}}</p></article>",
        "css": ".notice { padding: 24px; border-radius: 12px; background: white; }"
      }
    }

Preview templates must be static. Scripts, frames, embedded objects, forms, event-handler attributes, JavaScript URLs, remote URLs, external links, CSS imports, and style terminators are rejected. Templates have size limits and the rendered document applies a restrictive Content Security Policy. Providers can embed local images as data URLs; remote resources are not loaded.

A widget without preview remains editable and displays a message explaining that no local preview template is available. The local preview is a provider-authored representation, not the actual WordPress/Elementor PHP frontend.

## Recipe descriptor

A recipe contains id, name, format, optional description, formatVersion, and previewImage. The app does not assume every recipe format is JSON or PHP.

## Compatibility and safety

- Import reads a local JSON file and does not execute provider code.
- No WordPress server or internet connection is required.
- Manifest version mismatches, missing identifiers, duplicate IDs, and unsafe preview templates are rejected.
- The manifest grants no remote permissions and has no server-side side effects.
