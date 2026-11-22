using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyraStudio.Models;

namespace LyraStudio.Services;

public sealed record ProviderAdapterDiscoveryResult(
    string AdapterId,
    ProviderManifest Manifest,
    IReadOnlyList<string> Warnings);

public interface IProviderSourceAdapter
{
    string AdapterId { get; }
    bool CanHandle(IReadOnlyCollection<string> packageFiles);
    Task<ProviderAdapterDiscoveryResult?> DiscoverAsync(
        string packagePath,
        IReadOnlyCollection<string> packageFiles,
        ProviderPreviewAssets? previewAssets,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderAdapterRegistry
{
    private readonly IReadOnlyList<IProviderSourceAdapter> _adapters;

    public ProviderAdapterRegistry(IEnumerable<IProviderSourceAdapter>? adapters = null)
    {
        _adapters = (adapters ?? new IProviderSourceAdapter[] { new ElementorPhpSourceAdapter() }).ToArray();
    }

    public async Task<ProviderAdapterDiscoveryResult?> DiscoverAsync(
        string packagePath,
        IReadOnlyCollection<string> packageFiles,
        ProviderPreviewAssets? previewAssets,
        CancellationToken cancellationToken = default)
    {
        foreach (var adapter in _adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!adapter.CanHandle(packageFiles))
                continue;

            var result = await adapter.DiscoverAsync(packagePath, packageFiles, previewAssets, cancellationToken);
            if (result is not null)
                return result;
        }

        return null;
    }
}

/// <summary>
/// Read-only adapter for Elementor-compatible PHP widget packages. It discovers
/// an arbitrary number of widget classes and extracts the supported subset of
/// their controls without requiring a running WordPress/PHP installation.
/// </summary>
public sealed class ElementorPhpSourceAdapter : IProviderSourceAdapter
{
    private static readonly Regex ClassPattern = new(
        @"\bclass\s+(?<class>[a-zA-Z_][a-zA-Z0-9_]*)\s+extends\s+[^{]+\{",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RegistryEntryPattern = new(
        @"['""](?<class>[a-zA-Z_][a-zA-Z0-9_]*)['""]\s*=>\s*['""](?<path>[^'""]+\.php)['""]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly LyraCorePreviewRenderer _previewRenderer = new();

    public string AdapterId => "elementor.php-source.v1";

    public bool CanHandle(IReadOnlyCollection<string> packageFiles)
    {
        if (packageFiles.Any(path =>
            path.EndsWith("includes/class-lyra-elementor.php", StringComparison.OrdinalIgnoreCase)))
            return true;

        return packageFiles.Any(path =>
            path.EndsWith(".php", StringComparison.OrdinalIgnoreCase) &&
            path.Split('/').Any(segment =>
                segment.Equals("widgets", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("widget", StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<ProviderAdapterDiscoveryResult?> DiscoverAsync(
        string packagePath,
        IReadOnlyCollection<string> packageFiles,
        ProviderPreviewAssets? previewAssets,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var registryPath = packageFiles.FirstOrDefault(path =>
            path.EndsWith("includes/class-lyra-elementor.php", StringComparison.OrdinalIgnoreCase));
        var packageRoot = ResolvePackageRootPrefix(registryPath, packageFiles);
        var registeredWidgets = new List<(string ClassName, string RelativePath)>();

        if (registryPath is not null)
        {
            var registrySource = await ProviderPackageContentReader.ReadTextAsync(
                packagePath, registryPath, 2 * 1024 * 1024, cancellationToken);
            if (!string.IsNullOrWhiteSpace(registrySource))
            {
                foreach (Match match in RegistryEntryPattern.Matches(registrySource))
                {
                    var relative = match.Groups["path"].Value.Replace('\\', '/');
                    if (relative.Split('/').Any(segment => segment is "" or "." or "..") ||
                        relative.Contains(':'))
                    {
                        warnings.Add($"Skipped unsafe widget registry path: {relative}");
                        continue;
                    }

                    registeredWidgets.Add((
                        match.Groups["class"].Value,
                        packageRoot + "widgets/" + relative.TrimStart('/')));
                }
            }
        }

        if (registeredWidgets.Count == 0)
            warnings.Add("No static widget registry was found. Scanning PHP source files for classes that expose get_name() and register_controls().");

        // Scan the package's widget source directories as well as its registry.
        // This discovers additional compatible widgets shipped beside the core
        // registry, including extension widgets in the same plugin package.
        foreach (var path in packageFiles.Where(path =>
            path.EndsWith(".php", StringComparison.OrdinalIgnoreCase) &&
            (path.Contains("/widgets/", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("widgets/", StringComparison.OrdinalIgnoreCase))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = await ProviderPackageContentReader.ReadTextAsync(
                packagePath, path, 4 * 1024 * 1024, cancellationToken);
            if (string.IsNullOrWhiteSpace(source)) continue;

            var bodyMatch = ClassPattern.Match(source);
            if (!bodyMatch.Success) continue;

            var className = bodyMatch.Groups["class"].Value;
            if (PhpSourceAnalysis.ExtractMethodBody(source, "get_name") is null ||
                PhpSourceAnalysis.ExtractMethodBody(source, "register_controls") is null)
                continue;

            if (!registeredWidgets.Any(widget =>
                string.Equals(widget.ClassName, className, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(widget.RelativePath, path, StringComparison.OrdinalIgnoreCase)))
                registeredWidgets.Add((className, path));
        }

        var widgets = new List<WidgetDescriptor>();
        var seenWidgetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (className, widgetPath) in registeredWidgets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = await ProviderPackageContentReader.ReadTextAsync(
                packagePath, widgetPath, 4 * 1024 * 1024, cancellationToken);
            if (string.IsNullOrWhiteSpace(source))
            {
                warnings.Add($"Widget source file could not be read: {widgetPath}");
                continue;
            }

            var classMatch = ClassPattern.Match(source);
            if (classMatch.Success &&
                !string.Equals(classMatch.Groups["class"].Value, className, StringComparison.Ordinal))
            {
                warnings.Add($"Widget registry class '{className}' does not match source class in '{widgetPath}'.");
                continue;
            }

            var widgetId = ReadMethodString(source, "get_name");
            if (string.IsNullOrWhiteSpace(widgetId))
            {
                warnings.Add($"Skipped '{widgetPath}': get_name() does not return a static string.");
                continue;
            }

            if (!seenWidgetIds.Add(widgetId))
            {
                warnings.Add($"Skipped duplicate widget ID '{widgetId}'.");
                continue;
            }

            var title = ReadMethodString(source, "get_title");
            var description = ReadDocDescription(source);
            var controls = ExtractControls(source, warnings, widgetPath);
            var category = ResolveCategory(widgetPath);
            var rendererId = widgetId.StartsWith("lyra-", StringComparison.OrdinalIgnoreCase)
                ? "lyra-core.elementor/" + widgetId
                : "elementor.generic";

            widgets.Add(new WidgetDescriptor(
                widgetId,
                string.IsNullOrWhiteSpace(title) ? widgetId : title,
                category,
                description,
                null,
                controls,
                null)
            {
                PreviewRendererId = rendererId
            });
        }

        var pluginFile = packageFiles.FirstOrDefault(path =>
            string.Equals(path, packageRoot + "lyra-core.php", StringComparison.OrdinalIgnoreCase))
            ?? packageFiles.FirstOrDefault(path =>
                path.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(path).Equals("plugin.php", StringComparison.OrdinalIgnoreCase));
        var pluginSource = pluginFile is null
            ? null
            : await ProviderPackageContentReader.ReadTextAsync(packagePath, pluginFile, 128 * 1024, cancellationToken);

        var providerName = ReadPluginHeader(pluginSource, "Plugin Name") ?? "Elementor PHP Provider";
        var providerVersion = ReadPluginHeader(pluginSource, "Version") ?? "unknown";
        var providerId = MakeProviderId(providerName);

        var recipes = await DiscoverRecipesAsync(packagePath, packageFiles, widgets, previewAssets, warnings, cancellationToken);
        if (widgets.Count == 0 && recipes.Count == 0)
            return null;

        if (widgets.Any(widget => widget.PreviewRendererId == "elementor.generic"))
            warnings.Add("Some widgets use a structural settings preview because no provider-specific local renderer is available. Their source render() methods are not executed.");

        return new ProviderAdapterDiscoveryResult(
            AdapterId,
            new ProviderManifest
            {
                ManifestVersion = 1,
                ProviderId = providerId,
                ProviderName = providerName,
                ProviderVersion = providerVersion,
                Widgets = widgets,
                Recipes = recipes.ToList()
            },
            warnings);
    }

    private static IReadOnlyList<ControlDefinition> ExtractControls(
        string source,
        ICollection<string> warnings,
        string widgetPath)
    {
        var methodBody = PhpSourceAnalysis.ExtractMethodBody(source, "register_controls")
            ?? PhpSourceAnalysis.ExtractMethodBody(source, "_register_controls");
        if (methodBody is null)
            return Array.Empty<ControlDefinition>();

        var calls = PhpSourceAnalysis.FindControlCalls(methodBody);
        var groups = calls
            .Where(call => call.Target != "$this")
            .GroupBy(call => call.Target, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var controls = new List<ControlDefinition>();
        foreach (var call in calls.Where(call => call.Target == "$this"))
        {
            var definition = ToControlDefinition(call, warnings, widgetPath);
            if (definition.Kind.Equals("repeater", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(call.FieldsVariable) &&
                groups.TryGetValue("$" + call.FieldsVariable, out var childCalls))
            {
                definition = definition with
                {
                    Children = childCalls
                        .Select(child => ToControlDefinition(child, warnings, widgetPath))
                        .ToArray()
                };
            }
            controls.Add(definition);
        }

        AddTextStyleHelperControls(methodBody, controls);
        return controls;
    }

    private static ControlDefinition ToControlDefinition(
        PhpControlCall call,
        ICollection<string> warnings,
        string widgetPath)
    {
        var label = PhpSourceAnalysis.GetString(call.Config, "label") ?? call.Id;
        var type = PhpSourceAnalysis.GetString(call.Config, "type") ?? "unknown";
        var kind = MapControlKind(type);
        if (kind == "unknown")
            warnings.Add($"Control '{call.Id}' in '{widgetPath}' uses unsupported Elementor type '{type}'. Its value will be preserved.");

        var defaultValue = PhpSourceAnalysis.GetValue(call.Config, "default");
        var optionMap = PhpSourceAnalysis.GetValue(call.Config, "options") as IDictionary<string, object?>;
        IReadOnlyList<ControlOption>? options = optionMap?.Select(pair =>
            new ControlOption(
                Convert.ToString(pair.Value) ?? pair.Key,
                pair.Key)).ToArray();

        var conditionMap = PhpSourceAnalysis.GetValue(call.Config, "condition") as IDictionary<string, object?>;
        IReadOnlyList<ControlCondition>? conditions = conditionMap?.Select(pair =>
            new ControlCondition(pair.Key, "equals", pair.Value)).ToArray();

        var constraints = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "min", "max", "step", "min_items", "max_items" })
        {
            var value = PhpSourceAnalysis.GetValue(call.Config, key);
            if (value is not null) constraints[key] = value;
        }

        return new ControlDefinition
        {
            Name = call.Id,
            Label = label,
            Kind = kind,
            Description = PhpSourceAnalysis.GetString(call.Config, "description"),
            Required = PhpSourceAnalysis.GetBoolean(call.Config, "required"),
            DefaultValue = defaultValue,
            Options = options,
            Conditions = conditions,
            Constraints = constraints.Count == 0 ? null : constraints,
            IsResponsive = call.Method.Equals("add_responsive_control", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static void AddTextStyleHelperControls(
        string methodBody,
        ICollection<ControlDefinition> controls)
    {
        var pattern = new Regex(
            @"\$this\s*->\s*add_text_style\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var known = new HashSet<string>(controls.Select(control => control.Name), StringComparer.OrdinalIgnoreCase);
        foreach (Match match in pattern.Matches(methodBody))
        {
            var open = methodBody.IndexOf('(', match.Index + match.Length - 1);
            var close = PhpSourceAnalysis.FindMatching(methodBody, open, '(', ')');
            if (close < 0) continue;
            var args = PhpSourceAnalysis.SplitTopLevelArguments(methodBody[(open + 1)..close]);
            if (args.Count < 2) continue;

            var id = Convert.ToString(PhpStaticValueParser.ParseExpression(args[0])) ?? "";
            var label = Convert.ToString(PhpStaticValueParser.ParseExpression(args[1])) ?? id;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var colorId = id + "_color";
            if (known.Add(colorId))
                controls.Add(new ControlDefinition
                {
                    Name = colorId,
                    Label = label + " · Color",
                    Kind = "color"
                });

            var typographyId = id + "_typography";
            if (known.Add(typographyId))
                controls.Add(new ControlDefinition
                {
                    Name = typographyId,
                    Label = label + " · Typography",
                    Kind = "typography"
                });
        }
    }

    private static string MapControlKind(string type)
    {
        var value = type.Trim().ToUpperInvariant();
        if (value.Contains("::")) value = value[(value.LastIndexOf("::", StringComparison.Ordinal) + 2)..];

        return value switch
        {
            "TEXT" or "RAW_HTML" or "HIDDEN" => "text",
            "TEXTAREA" or "WYSIWYG" => "textarea",
            "NUMBER" => "number",
            "SWITCHER" => "toggle",
            "SELECT" or "SELECT2" or "CHOOSE" => "select",
            "COLOR" or "COLOR_PICKER" => "color",
            "URL" => "url",
            "MEDIA" => "media",
            "ICONS" => "icon",
            "REPEATER" => "repeater",
            "DIMENSIONS" => "dimensions",
            "SLIDER" => "slider",
            "DATE_TIME" => "date",
            "POPOVER_TOGGLE" or "FONT" => "typography",
            _ => "unknown"
        };
    }

    private async Task<IReadOnlyList<RecipeDescriptor>> DiscoverRecipesAsync(
        string packagePath,
        IReadOnlyCollection<string> packageFiles,
        IReadOnlyList<WidgetDescriptor> widgets,
        ProviderPreviewAssets? previewAssets,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var recipeFiles = packageFiles
            .Where(path => path.StartsWith(FindPackageRootPrefix(packageFiles) + "includes/recipes/", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(path).Equals("index.php", StringComparison.OrdinalIgnoreCase) &&
                (path.EndsWith(".php", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lookup = widgets.ToDictionary(widget => widget.Id, StringComparer.OrdinalIgnoreCase);
        var recipes = new List<RecipeDescriptor>();

        foreach (var path in recipeFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = await ProviderPackageContentReader.ReadTextAsync(
                packagePath, path, 4 * 1024 * 1024, cancellationToken);
            if (string.IsNullOrWhiteSpace(source)) continue;

            object? parsed;
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<object>(source);
                    if (parsed is JsonElement json)
                        parsed = JsonElementToObject(json);
                }
                catch (JsonException)
                {
                    warnings.Add($"Skipped invalid JSON recipe: {path}");
                    continue;
                }
            }
            else
            {
                parsed = PhpStaticValueParser.ParseReturnExpression(source);
            }

            if (parsed is not IDictionary<string, object?> recipe)
            {
                warnings.Add($"Recipe '{path}' contains dynamic PHP expressions and could not be statically parsed.");
                continue;
            }

            var slug = PhpSourceAnalysis.GetString(recipe, "slug");
            var title = PhpSourceAnalysis.GetString(recipe, "title") ?? slug;
            var elements = PhpSourceAnalysis.GetValue(recipe, "elements");
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(title) || elements is null)
            {
                warnings.Add($"Recipe '{path}' is missing a static slug, title, or elements array.");
                continue;
            }

            var previewHtml = _previewRenderer.RenderRecipeElements(elements, lookup, previewAssets);
            recipes.Add(new RecipeDescriptor(
                slug,
                title,
                PhpSourceAnalysis.GetString(recipe, "description"),
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "lyra-page-recipe-php",
                "1",
                null)
            {
                PreviewHtml = previewHtml
            });
        }

        return recipes;
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => JsonElementToObject(property.Value),
            StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.ToString()
    };

    private static string ResolvePackageRootPrefix(
        string? registryPath,
        IReadOnlyCollection<string> packageFiles)
    {
        const string marker = "includes/class-lyra-elementor.php";
        if (registryPath is not null)
        {
            var index = registryPath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return registryPath[..index];
        }
        return FindPackageRootPrefix(packageFiles);
    }

    private static string FindPackageRootPrefix(IReadOnlyCollection<string> packageFiles)
    {
        var widgetFile = packageFiles.FirstOrDefault(path =>
            path.Contains("/widgets/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("widgets/", StringComparison.OrdinalIgnoreCase));
        if (widgetFile is null) return "";

        var marker = widgetFile.IndexOf("widgets/", StringComparison.OrdinalIgnoreCase);
        return marker < 0 ? "" : widgetFile[..marker];
    }

    private static string? ReadMethodString(string source, string methodName)
    {
        var body = PhpSourceAnalysis.ExtractMethodBody(source, methodName);
        if (body is null) return null;
        var parsed = PhpStaticValueParser.ParseReturnExpression(body);
        return parsed is string text ? text : Convert.ToString(parsed);
    }

    private static string? ReadDocDescription(string source)
    {
        var match = Regex.Match(source, @"/\*\*(?<doc>.*?)\*/", RegexOptions.Singleline);
        if (!match.Success) return null;

        var lines = match.Groups["doc"].Value.Split('\n')
            .Select(line => Regex.Replace(line.Trim(), @"^\*\s?", ""))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith('@'))
            .ToList();
        return lines.Count == 0 ? null : string.Join(" ", lines);
    }

    private static string ResolveCategory(string path)
    {
        var normalized = path.Replace('\\', '/');
        var marker = normalized.IndexOf("widgets/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return "Elementor";
        var remainder = normalized[(marker + "widgets/".Length)..];
        var slash = remainder.IndexOf('/');
        return slash < 0 ? "Elementor" : remainder[..slash];
    }

    private static string? ReadPluginHeader(string? source, string field)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var match = Regex.Match(source, $@"^\s*\*?\s*{Regex.Escape(field)}:\s*(?<value>[^\r\n]+)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string MakeProviderId(string providerName)
    {
        var normalized = Regex.Replace(providerName.ToLowerInvariant(), @"[^a-z0-9]+", ".").Trim('.');
        return string.IsNullOrWhiteSpace(normalized) ? "elementor.php-provider" : normalized;
    }
}
