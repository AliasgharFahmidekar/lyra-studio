using System.IO;
using System.IO.Compression;
using System.Text;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class ProviderPackageImporterTests
{
    private readonly ProviderPackageImporter _importer = new();

    [Fact]
    public async Task ReadsProviderManifestFromZipWithoutExtractingPackage()
    {
        var path = await CreateZipAsync(new Dictionary<string, string>
        {
            ["lyra-studio.provider.json"] = ValidManifest,
            ["src/plugin.php"] = "<?php // source",
            ["assets/logo.png"] = "not-a-real-image",
            ["templates/card.html"] = "<article>Card</article>"
        });

        try
        {
            var result = await _importer.InspectAsync(path);

            Assert.True(result.HasProviderManifest);
            Assert.Equal("sample.provider", result.Manifest!.ProviderId);
            Assert.Single(result.Manifest.Widgets);
            Assert.Equal("src/plugin.php", Assert.Single(result.Files.Where(x => x.Category == "Source")).RelativePath);
            Assert.Contains(result.Files, x => x.Category == "Asset");
            Assert.Contains(result.Files, x => x.Category == "Template");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ScansUnknownPluginZipAsGenericInventory()
    {
        var path = await CreateZipAsync(new Dictionary<string, string>
        {
            ["plugin/main.php"] = "<?php // unknown plugin",
            ["plugin/assets/icon.svg"] = "<svg/>",
            ["plugin/templates/hero.html"] = "<section/>"
        });

        try
        {
            var result = await _importer.InspectAsync(path);

            Assert.Null(result.Manifest);
            Assert.Equal(3, result.FileCount);
            Assert.Contains(result.Warnings, warning => warning.Contains("provider adapter", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Files, x => x.Category == "Source");
            Assert.Contains(result.Files, x => x.Category == "Asset");
            Assert.Contains(result.Files, x => x.Category == "Template");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RejectsZipSlipPaths()
    {
        var path = await CreateZipAsync(new Dictionary<string, string>
        {
            ["../outside.php"] = "<?php // unsafe"
        });

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => _importer.InspectAsync(path));
            Assert.Contains("unsafe path", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RejectsAmbiguousNestedProviderManifests()
    {
        var path = await CreateZipAsync(new Dictionary<string, string>
        {
            ["one/lyra-studio.provider.json"] = ValidManifest,
            ["two/provider-manifest.json"] = ValidManifest
        });

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => _importer.InspectAsync(path));
            Assert.Contains("multiple provider manifests", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportsSourceFolderAndFindsNestedManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lyra-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "plugin", "src"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "plugin", "lyra-studio.provider.json"), ValidManifest);
            await File.WriteAllTextAsync(Path.Combine(root, "plugin", "src", "plugin.php"), "<?php // source");

            var result = await _importer.InspectAsync(root);

            Assert.True(result.HasProviderManifest);
            Assert.Equal("plugin/lyra-studio.provider.json", result.ManifestEntryPath);
            Assert.Contains(result.Files, x => x.Category == "Source");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> CreateZipAsync(IReadOnlyDictionary<string, string> entries)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lyra-package-{Guid.NewGuid():N}.zip");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                await using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes);
            }
        }
        return path;
    }

    private const string ValidManifest = """
    {
      "manifestVersion": 1,
      "providerId": "sample.provider",
      "providerName": "Sample Provider",
      "providerVersion": "1.0.0",
      "widgets": [{ "id": "sample.card", "name": "Sample Card" }],
      "recipes": [{ "id": "sample.hero", "name": "Sample Hero", "format": "sample-document-v1" }]
    }
    """;
}
