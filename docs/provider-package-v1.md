# Provider Package v1

Lyra Studio accepts a local provider package as a ZIP archive or an unpacked source folder. It also continues to accept the standalone JSON manifest used by earlier versions.

## Package layout

A package may contain any project-specific source, assets, templates, documentation, or other files. A provider that wants Lyra Studio to expose its widget and recipe catalog places this file in the package:

    lyra-studio.provider.json

The manifest uses the existing Provider Manifest v1 schema. It declares stable provider identity, widgets, control schemas, optional local widget previews, and recipe descriptors. The manifest may be at the package root or be the only matching manifest inside a single plugin root folder.

Example package:

    sample-plugin/
      lyra-studio.provider.json
      src/
      assets/
      templates/
      recipes/

A package may use different directory names and languages. The package importer does not assume PHP, WordPress, Elementor, or any particular source layout.

## What import does

- ZIP files are inspected in place; they are not extracted to arbitrary paths.
- Source folders are traversed without following symbolic links or Windows reparse points.
- Files are inventoried and categorized as source, asset, template, recipe/page data, style, or other.
- If a provider manifest is found, it is validated and the widget/recipe metadata is loaded.
- If no manifest is found, Lyra Studio displays a generic package inventory and explains that a provider-specific discovery adapter is needed.
- A standalone JSON Provider Manifest v1 remains supported.

## Safety limits

- Maximum 10,000 files/entries.
- Maximum nesting depth of 32 folders for source-folder scans.
- Maximum 128 MiB per file and 1 GiB total declared package content.
- Maximum 8 MiB provider manifest.
- Absolute paths, drive-qualified paths, traversal segments (two-dot segments), alternate data stream colons, duplicate case-insensitive paths, symlink and reparse-point ZIP entries are rejected.
- Linked items in a source folder are skipped and reported.
- Package files are never executed. PHP, JavaScript, executables and other source files are treated as inert data.

These checks are package-inspection safeguards, not a sandbox for running imported source code. Lyra Studio does not execute plugin code.

## Discovery boundary

The manifest is the provider-neutral interchange contract. It is how a package exposes widget definitions and recipe descriptors without requiring the Studio core to understand each programming language.

A generic file scan can identify likely assets, templates, source files and content files, but it cannot reliably infer a widget's complete control schema or execute its real frontend renderer. Those capabilities belong in optional provider-specific adapters and are planned as a separate milestone. Unknown source elements must be reported, not silently discarded or guessed.

A recipe descriptor currently identifies the recipe and its source format. It is not yet a parsed editable page tree. Full page/template/section/widget document parsing is a later milestone.

## Site data versus plugin source

A plugin ZIP and a saved site-content export are different inputs. Pages, user-created templates, and recipes stored in a site's database may not exist in the plugin's source package. Users must provide an export/package of that content when they want Studio to inspect those saved documents. Importing a plugin archive alone must never be represented as importing every page stored by an external site.
