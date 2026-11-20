namespace LyraStudio.Models;

/// <summary>
/// Read-only capabilities discovered from a site's public WordPress REST index.
/// Route presence does not imply permission to call a route or support for preview/save.
/// </summary>
public sealed record WordPressSiteCapabilities(
    Uri RestApiRoot,
    IReadOnlyList<string> Namespaces,
    IReadOnlyList<string> ElementorRoutes,
    int DiscoveredRouteCount,
    bool HasWordPressPagesRoute,
    bool HasElementorNamespace,
    bool HasLyraNamespace)
{
    public string ToDisplayText()
    {
        var pages = HasWordPressPagesRoute
            ? "WordPress Pages route: declared"
            : "WordPress Pages route: not found";
        var elementor = HasElementorNamespace
            ? $"Elementor REST namespace: found ({ElementorRoutes.Count} route(s) listed)"
            : "Elementor REST namespace: not listed";
        var lyra = HasLyraNamespace
            ? "Lyra REST namespace: found"
            : "Lyra REST namespace: not found";

        return string.Join(Environment.NewLine,
            $"REST API root: {RestApiRoot}",
            $"Namespaces: {Namespaces.Count} · Routes: {DiscoveredRouteCount}",
            pages,
            elementor,
            lyra,
            "Live preview capability: not established by REST discovery alone.",
            "This check was read-only; no page, post, recipe, or plugin data was changed.");
    }
}
