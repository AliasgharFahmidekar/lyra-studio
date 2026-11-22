# Provider Package v1

Lyra Studio accepts a local provider package as a ZIP archive or an unpacked source folder. It also continues to accept the standalone JSON manifest used by earlier versions.

## Package layout

A package may contain any project-specific source, assets, templates, documentation, or other files. A provider that wants Lyra Studio to expose its catalog can place this file in the package:

    lyra-studio.provider.json

The manifest uses the existing Provider Manifest v1 schema. It declares stable provider identity, widgets, control schemas, optional local widget previews, and recipe descriptors. The manifest may be at the package root or be the only matching manifest inside a single plugin root folder.

A package does not have to include a Lyra Studio manifest when a built-in or installed source adapter recognizes its structure.

## What import does

- ZIP files are inspected in place; they are not extracted to arbitrary paths.
- Source folders are traversed without following symbolic links or Windows reparse points.
- Files are inventoried and categorized as source, asset, template, recipe/page data, style, or other.
- If a provider manifest is found, it is validated and the widget/recipe metadata is loaded.
- If no manifest is found, the adapter registry checks for a compatible static source structure.
- The first built-in source adapter recognizes Elementor-compatible PHP widget folders/registries and Lyra Page Builder recipe files.
- Widget count is discovered from package source/registry data and is not fixed to a hardcoded list.
- A standalone JSON Provider Manifest v1 remains supported.

## Preview-first adapter behavior

The first adapter reads static class/file registries and compatible widget classes, then inspects:
- get_name() and get_title() return expressions;
- register_controls() and add_control() / add_responsive_control() declarations;
- labels, control kinds, defaults, options, simple conditions, responsive markers and repeater child schemas;
- Lyra Page Builder recipe arrays using container() and widget() expressions.

Static recipe data is converted into a local nested preview representation. The built-in Lyra Core renderer has native preview mappings for the five known widget IDs in Lyra Core 0.1.2. Other compatible widgets are discovered and exposed in the Inspector with a structural preview until a visual renderer is available.

Package-local CSS, font files, bounded image assets and safe SVG symbol sprites can be used by the local preview. External resources are not fetched.

## Safety limits

- Maximum 10,000 files/entries.
- Maximum nesting depth of 32 folders for source-folder scans.
- Maximum 128 MiB per file and 1 GiB total declared package content.
- Maximum 8 MiB provider manifest.
- Absolute paths, drive-qualified paths, traversal segments (..) and alternate data stream colons, duplicate case-insensitive paths, symlink and reparse-point ZIP entries are rejected.
- Linked items in a source folder are skipped and reported.
- Package files are never executed. PHP, JavaScript, executables and other source files are treated as inert data.

The static PHP reader supports a bounded subset of expressions. It does not evaluate arbitrary PHP, hooks, dynamic callbacks, site/database values, WooCommerce data, or JavaScript templates. These limits are reported rather than hidden.

These checks are package-inspection safeguards, not a sandbox for running imported source code. Lyra Studio does not execute plugin code.

## Discovery and preview boundary

A generic file scan can identify likely assets, templates, source files and content files, but it cannot infer every provider's semantic model. Additional ecosystems need their own adapters. Source adapters translate native declarations into the common Studio descriptors; they do not add provider-specific parsing logic to the desktop UI.

A structural preview is not a faithful frontend render. A native preview is a Studio-generated local representation and must not be described as pixel-identical to a PHP/Elementor frontend.

## Site data versus plugin source

A plugin ZIP and a saved site-content export are different inputs. Pages, user-created templates, and recipes stored in a site's database may not exist in the plugin's source package. Users must provide an export/package of that content when they want Studio to inspect those saved documents. Importing a plugin archive alone must never be represented as importing every page stored by an external site.
