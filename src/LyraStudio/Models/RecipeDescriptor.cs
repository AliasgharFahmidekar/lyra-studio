namespace LyraStudio.Models;

public sealed record RecipeDescriptor(
    string Id,
    string Name,
    string? Description,
    string Format,
    string? FormatVersion = null,
    string? PreviewImage = null)
{
    /// <summary>Prebuilt, inert HTML fragment for local preview of this recipe.</summary>
    public string? PreviewHtml { get; init; }
}
