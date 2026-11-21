namespace LyraStudio.Models;

public sealed record ProviderPackageInspection(
    string PackageName,
    string PackagePath,
    ProviderManifest? Manifest,
    string? ManifestEntryPath,
    IReadOnlyList<PackageFileDescriptor> Files,
    IReadOnlyList<string> Warnings)
{
    public ProviderPreviewAssets? PreviewAssets { get; init; }
    public string? AdapterId { get; init; }
    public bool HasProviderManifest => Manifest is not null;
    public int FileCount => Files.Count;
    public int WidgetCount => Manifest?.Widgets.Count ?? 0;
    public int RecipeCount => Manifest?.Recipes.Count ?? 0;
}

public sealed record PackageFileDescriptor(
    string RelativePath,
    long SizeBytes,
    string Category,
    string Extension)
{
    public string DisplayName => $"{Category}  ·  {RelativePath}  ·  {SizeBytes:N0} bytes";
}
