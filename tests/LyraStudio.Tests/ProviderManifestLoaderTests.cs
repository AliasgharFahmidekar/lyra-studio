using System.IO;
using System.Text;
using LyraStudio.Models;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class ProviderManifestLoaderTests
{
    private readonly ProviderManifestLoader _loader = new();

    [Fact]
    public async Task LoadsValidManifestAndPreservesUnknownControlKinds()
    {
        const string json = """
        {
          "manifestVersion": 1,
          "providerId": "test.provider",
          "providerName": "Test Provider",
          "widgets": [{
            "id": "test.card",
            "name": "Test Card",
            "controls": [{
              "name": "future_control",
              "label": "Future control",
              "kind": "brand_new_control",
              "defaultValue": "kept"
            }]
          }],
          "recipes": []
        }
        """;

        var manifest = await LoadFromText(json);

        Assert.Equal("test.provider", manifest.ProviderId);
        Assert.Single(manifest.Widgets);
        Assert.Equal("brand_new_control", manifest.Widgets[0].Controls![0].Kind);
    }

    [Fact]
    public async Task RejectsUnsupportedManifestVersion()
    {
        const string json = """
        { "manifestVersion": 99, "providerId": "test", "providerName": "Test", "widgets": [], "recipes": [] }
        """;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => LoadFromText(json));
        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public async Task RejectsDuplicateWidgetIdentifiersCaseInsensitively()
    {
        const string json = """
        {
          "manifestVersion": 1,
          "providerId": "test",
          "providerName": "Test",
          "widgets": [
            { "id": "hero", "name": "Hero" },
            { "id": "HERO", "name": "Hero two" }
          ],
          "recipes": []
        }
        """;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => LoadFromText(json));
        Assert.Contains("Duplicate widget", exception.Message);
    }

    [Fact]
    public async Task RejectsDuplicateNestedControlIdentifiers()
    {
        const string json = """
        {
          "manifestVersion": 1,
          "providerId": "test",
          "providerName": "Test",
          "widgets": [{
            "id": "test.card",
            "name": "Test Card",
            "controls": [{
              "name": "layout",
              "label": "Layout",
              "kind": "group",
              "children": [
                { "name": "columns", "label": "Columns", "kind": "number" },
                { "name": "COLUMNS", "label": "Again", "kind": "number" }
              ]
            }]
          }],
          "recipes": []
        }
        """;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => LoadFromText(json));
        Assert.Contains("Duplicate control", exception.Message);
    }

    private async Task<ProviderManifest> LoadFromText(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lyra-manifest-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);
            return await _loader.LoadAsync(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
