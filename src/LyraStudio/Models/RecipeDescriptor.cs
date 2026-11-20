namespace LyraStudio.Models;

public sealed record RecipeDescriptor(
    string Id,
    string Name,
    string? Description,
    string Format,
    string? FormatVersion = null,
    string? PreviewImage = null);
