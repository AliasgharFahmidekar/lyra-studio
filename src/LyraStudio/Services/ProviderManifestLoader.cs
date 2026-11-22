using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Loads and validates a provider manifest from a local file or package stream.
/// Importing metadata never executes provider code.
/// </summary>
public sealed class ProviderManifestLoader
{
    public const int MaximumManifestBytes = 8 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public async Task<ProviderManifest> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A manifest file path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Provider manifest file was not found.", path);

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaximumManifestBytes)
            throw new InvalidDataException($"The provider manifest exceeds the {MaximumManifestBytes:N0}-byte limit.");

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await LoadAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Reads a bounded manifest from a stream without taking ownership of the stream.
    /// This supports reading metadata directly from a ZIP entry without extracting files.
    /// </summary>
    public async Task<ProviderManifest> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The manifest stream must be readable.", nameof(stream));

        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumManifestBytes)
                throw new InvalidDataException($"The provider manifest exceeds the {MaximumManifestBytes:N0}-byte limit.");

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
            throw new InvalidDataException("The manifest file is empty.");

        buffer.Position = 0;
        ProviderManifest manifest;
        try
        {
            manifest = await JsonSerializer.DeserializeAsync<ProviderManifest>(
                buffer, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The manifest file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The manifest JSON is invalid: {ex.Message}", ex);
        }

        Validate(manifest);
        return manifest;
    }

    private static void Validate(ProviderManifest manifest)
    {
        if (manifest.ManifestVersion != 1)
            throw new InvalidDataException(
                $"Manifest version {manifest.ManifestVersion} is not supported. Supported version: 1.");

        if (string.IsNullOrWhiteSpace(manifest.ProviderId))
            throw new InvalidDataException("ProviderId is required.");
        if (string.IsNullOrWhiteSpace(manifest.ProviderName))
            throw new InvalidDataException("ProviderName is required.");
        if (manifest.Widgets is null || manifest.Recipes is null)
            throw new InvalidDataException("Widgets and Recipes must be arrays; use empty arrays when there are no entries.");

        EnsureUnique(manifest.Widgets.Select(x => x.Id), "widget");
        EnsureUnique(manifest.Recipes.Select(x => x.Id), "recipe");

        foreach (var widget in manifest.Widgets)
        {
            if (string.IsNullOrWhiteSpace(widget.Id) || string.IsNullOrWhiteSpace(widget.Name))
                throw new InvalidDataException("Every widget must have a non-empty Id and Name.");
            ValidateControls(widget.Controls);
            ValidatePreview(widget.Preview);
        }

        foreach (var recipe in manifest.Recipes)
        {
            if (string.IsNullOrWhiteSpace(recipe.Id) ||
                string.IsNullOrWhiteSpace(recipe.Name) ||
                string.IsNullOrWhiteSpace(recipe.Format))
                throw new InvalidDataException("Every recipe must have an Id, Name, and Format.");
        }
    }

    private static void ValidatePreview(WidgetPreviewDefinition? preview)
    {
        if (preview is null) return;
        if (preview.Html.Length > 262144 || preview.Css.Length > 131072)
            throw new InvalidDataException("A local widget preview template exceeds the supported size limit.");

        if (Regex.IsMatch(preview.Html,
            @"<\s*(script|iframe|object|embed|form)\b|\son[a-z]+\s*=|javascript\s*:|https?://|<\s*link\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidDataException("Local preview HTML contains a forbidden script, frame, event handler, link, or remote URL.");

        if (Regex.IsMatch(preview.Css,
            @"@import\b|url\s*\(\s*['""]?https?://|</style",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidDataException("Local preview CSS contains a forbidden import, remote URL, or style terminator.");
    }

    private static void ValidateControls(IReadOnlyList<ControlDefinition>? controls)
    {
        if (controls is null) return;
        EnsureUnique(controls.Select(x => x.Name), "control in the same group");
        foreach (var control in controls)
        {
            if (string.IsNullOrWhiteSpace(control.Name) || string.IsNullOrWhiteSpace(control.Label))
                throw new InvalidDataException("Every control must have a non-empty Name and Label.");
            ValidateControls(control.Children);
        }
    }

    private static void EnsureUnique(IEnumerable<string> values, string kind)
    {
        var duplicate = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new InvalidDataException($"Duplicate {kind} identifier: '{duplicate.Key}'.");
    }
}
