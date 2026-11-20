using System.Text.Json;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class SettingsJsonSerializerTests
{
    [Fact]
    public void ExpandsDottedGroupPathsIntoNestedObjects()
    {
        var settings = new Dictionary<string, object?>
        {
            ["title"] = "Heading",
            ["layout.columns"] = 3,
            ["layout.gap"] = 24
        };

        using var document = JsonDocument.Parse(SettingsJsonSerializer.Serialize(settings));

        Assert.Equal("Heading", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("layout").GetProperty("columns").GetInt32());
        Assert.Equal(24, document.RootElement.GetProperty("layout").GetProperty("gap").GetInt32());
    }

    [Fact]
    public void PreservesRepeaterArraysAndExpandsNestedItemGroups()
    {
        var item = new Dictionary<string, object?>
        {
            ["title"] = "Feature",
            ["appearance.icon"] = "star"
        };
        var settings = new Dictionary<string, object?> { ["features"] = new List<Dictionary<string, object?>> { item } };

        using var document = JsonDocument.Parse(SettingsJsonSerializer.Serialize(settings));
        var feature = document.RootElement.GetProperty("features")[0];

        Assert.Equal("Feature", feature.GetProperty("title").GetString());
        Assert.Equal("star", feature.GetProperty("appearance").GetProperty("icon").GetString());
    }
}
