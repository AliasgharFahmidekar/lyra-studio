namespace LyraStudio.Models;

/// <summary>
/// Versioned, plugin-neutral description of a provider's widgets and recipes.
/// A provider may generate this document without coupling its implementation to WPF.
/// </summary>
public sealed record ProviderManifest
{
    public int ManifestVersion { get; init; } = 1;
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public string ProviderVersion { get; init; } = "unknown";
    public List<WidgetDescriptor> Widgets { get; init; } = new();
    public List<RecipeDescriptor> Recipes { get; init; } = new();
}
