# Provider Manifest v1

Lyra Studio can import a provider manifest as a read-only discovery document. The contract belongs to Lyra Studio and does not require Lyra Core to change now. A future adapter may generate this document from a plugin, local project, or another compatible provider.

## Top-level fields

| Field | Type | Required | Meaning |
|---|---|---:|---|
| manifestVersion | integer | Yes | Contract version. Currently `1`. |
| providerId | string | Yes | Stable provider identifier. |
| providerName | string | Yes | Human-readable provider name. |
| providerVersion | string | No | Provider release/version. |
| widgets | array | No | Discovered widget descriptors. |
| recipes | array | No | Discovered recipe descriptors. |

## Widget descriptor

A widget contains `id`, `name`, optional `category`, `description`, `version`, and `controls`.

Each control contains a stable `name`, display `label`, string `kind`, optional `description`, `required`, `defaultValue`, `options`, `children`, `conditions`, `constraints`, and `isResponsive`.

Control kinds currently recognized by the desktop inspector include: `text`, `textarea`, `number`, `toggle`, `select`, `color`, `url`, `media`, `icon`, `group`, `repeater`, `typography`, `dimensions`, `slider`, and `date`. Unknown kinds are retained and displayed as raw text/JSON instead of being dropped.

A repeater control uses its `children` array as its per-item field schema. Lyra Studio renders each item as a card and supports adding, removing, and reordering items. Each item's nested fields are generated from the same control schema. The `constraints.min_items` and `constraints.max_items` values enforce collection limits. If `max_items` is omitted, the desktop editor defaults to 100; a value of 0 means no configured maximum.

## Recipe descriptor

A recipe contains `id`, `name`, `format`, optional `description`, `formatVersion`, and `previewImage`. The app does not assume that every recipe is Elementor JSON or PHP.

## Example

The following is an illustrative contract example only. It is not a hardcoded widget shipped by Lyra Studio and does not claim to represent any current Lyra Core widget.

```json
{
  "manifestVersion": 1,
  "providerId": "example.provider",
  "providerName": "Example Provider",
  "providerVersion": "0.1.0",
  "widgets": [
    {
      "id": "example.notice",
      "name": "Notice",
      "category": "Content",
      "description": "Illustrative widget used to explain the manifest format.",
      "controls": [
        {
          "name": "title",
          "label": "Title",
          "kind": "text",
          "required": true,
          "defaultValue": "Hello"
        },
        {
          "name": "show_link",
          "label": "Show link",
          "kind": "toggle",
          "defaultValue": false
        },
        {
          "name": "link_text",
          "label": "Link text",
          "kind": "text",
          "defaultValue": "Learn more",
          "conditions": [
            { "field": "show_link", "operator": "equals", "value": true }
          ]
        },
        {
          "name": "items",
          "label": "Items",
          "kind": "repeater",
          "defaultValue": []
        },
        {
          "name": "layout",
          "label": "Layout",
          "kind": "group",
          "children": [
            {
              "name": "columns",
              "label": "Columns",
              "kind": "select",
              "defaultValue": "2",
              "options": [
                { "label": "Two", "value": "2" },
                { "label": "Three", "value": "3" }
              ]
            }
          ]
        }
      ]
    }
  ],
  "recipes": [
    {
      "id": "example.landing",
      "name": "Example Landing",
      "description": "Illustrative recipe descriptor.",
      "format": "example-json",
      "formatVersion": "1"
    }
  ]
}
```

## Safety and compatibility

- Importing a manifest only reads the selected local JSON file.
- It does not execute provider code, PHP, JavaScript, or recipe content.
- Manifest version mismatch, missing required identifiers, and duplicate widget/recipe/control identifiers are rejected with a readable validation error.
- A manifest describes controls; it does not grant the application a live preview endpoint or permission to write to WordPress.
