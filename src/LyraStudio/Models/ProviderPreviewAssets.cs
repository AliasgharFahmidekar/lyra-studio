namespace LyraStudio.Models;

/// <summary>
/// Local-only resources read from an imported provider package and embedded
/// into preview documents. Nothing is written back to the package.
/// </summary>
public sealed record ProviderPreviewAssets
{
    public string StylesheetCss { get; init; } = "";
    public string? IconSpriteMarkup { get; init; }
    public IReadOnlySet<string> IconNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> DataAssets { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? ResolveAsset(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var key = reference.StartsWith("asset://", StringComparison.OrdinalIgnoreCase)
            ? reference["asset://".Length..]
            : reference.TrimStart('/', '\\');

        return DataAssets.TryGetValue(key.Replace('\\', '/'), out var uri) ? uri : null;
    }
}
