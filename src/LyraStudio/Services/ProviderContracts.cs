using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Provider boundary for local widget and recipe catalogs. Providers may read
/// manifests and assets from disk; the desktop app does not require a server.
/// </summary>
public interface IStudioProvider
{
    string ProviderId { get; }
    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
}

public sealed record ProviderCapabilities(
    string ProviderName,
    string ProviderVersion,
    bool SupportsWidgetDiscovery,
    bool SupportsRecipeDiscovery,
    bool SupportsLocalPreview,
    bool SupportsRecipeImport,
    bool SupportsRecipeExport);

public interface IWidgetCatalogProvider : IStudioProvider
{
    Task<IReadOnlyList<WidgetDescriptor>> GetWidgetsAsync(CancellationToken cancellationToken = default);
}

public interface IRecipeCatalogProvider : IStudioProvider
{
    Task<IReadOnlyList<RecipeDescriptor>> GetRecipesAsync(CancellationToken cancellationToken = default);
}
