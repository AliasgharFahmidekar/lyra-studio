using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Provider boundary for future Lyra Core and other compatible widget sources.
/// Implementations must report capabilities instead of assuming fixed plugin features.
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
    bool SupportsLivePreview,
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

public interface IPreviewProvider : IStudioProvider
{
    Task<PreviewSession> CreatePreviewAsync(PreviewRequest request, CancellationToken cancellationToken = default);
    Task ReleasePreviewAsync(string sessionId, CancellationToken cancellationToken = default);
}
