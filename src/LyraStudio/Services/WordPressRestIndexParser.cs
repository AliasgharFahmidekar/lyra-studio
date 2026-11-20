using System.IO;
using System.Text.Json;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>Parses the public WordPress REST index without making assumptions about route permissions.</summary>
public static class WordPressRestIndexParser
{
    public static WordPressSiteCapabilities Parse(string json, Uri restApiRoot)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("The WordPress REST index response was empty.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The WordPress REST index must be a JSON object.");

        var namespaces = ReadStringArray(root, "namespaces");
        var routes = root.TryGetProperty("routes", out var routesElement) &&
                     routesElement.ValueKind == JsonValueKind.Object
            ? routesElement
            : throw new InvalidDataException("The WordPress REST index does not contain a routes object.");

        var routeNames = routes.EnumerateObject().Select(x => x.Name).ToArray();
        var elementorRoutes = routeNames
            .Where(x => x.StartsWith("/elementor/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasPagesRoute = routeNames.Any(IsWordPressPagesRoute);
        var hasElementorNamespace = namespaces.Any(x =>
            x.Equals("elementor", StringComparison.OrdinalIgnoreCase) ||
            x.StartsWith("elementor/", StringComparison.OrdinalIgnoreCase));
        var hasLyraNamespace = namespaces.Any(x =>
            x.StartsWith("lyra/", StringComparison.OrdinalIgnoreCase) ||
            x.StartsWith("lyra-core/", StringComparison.OrdinalIgnoreCase) ||
            x.StartsWith("lyra-studio/", StringComparison.OrdinalIgnoreCase));

        return new WordPressSiteCapabilities(
            restApiRoot,
            namespaces,
            elementorRoutes,
            routeNames.Length,
            hasPagesRoute,
            hasElementorNamespace,
            hasLyraNamespace);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return property.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsWordPressPagesRoute(string route) =>
        route.Equals("/wp/v2/pages", StringComparison.OrdinalIgnoreCase) ||
        route.StartsWith("/wp/v2/pages/", StringComparison.OrdinalIgnoreCase);
}
