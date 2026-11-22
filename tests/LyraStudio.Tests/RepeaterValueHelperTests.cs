using System.Text.Json;
using LyraStudio.Models;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class RepeaterValueHelperTests
{
    private static readonly IReadOnlyList<ControlDefinition> Schema =
    [
        new ControlDefinition { Name = "title", Label = "Title", Kind = "text", DefaultValue = "New item" },
        new ControlDefinition
        {
            Name = "layout",
            Label = "Layout",
            Kind = "group",
            Children = [new ControlDefinition { Name = "size", Label = "Size", Kind = "number", DefaultValue = 3 }]
        }
    ];

    [Fact]
    public void CreatesNewItemsWithSchemaDefaults()
    {
        var item = RepeaterValueHelper.CreateDefaultItem(Schema);

        Assert.Equal("New item", item["title"]);
        Assert.Equal(3, item["layout.size"]);
    }

    [Fact]
    public void AddsAndEnforcesMaximum()
    {
        var items = new List<Dictionary<string, object?>>();
        Assert.True(RepeaterValueHelper.TryAdd(items, Schema, maximum: 1));
        Assert.False(RepeaterValueHelper.TryAdd(items, Schema, maximum: 1));
        Assert.Single(items);
    }

    [Fact]
    public void RemovesAndEnforcesMinimum()
    {
        var items = new List<Dictionary<string, object?>>
        {
            RepeaterValueHelper.CreateDefaultItem(Schema),
            RepeaterValueHelper.CreateDefaultItem(Schema)
        };

        Assert.True(RepeaterValueHelper.TryRemove(items, 0, minimum: 1));
        Assert.False(RepeaterValueHelper.TryRemove(items, 0, minimum: 1));
        Assert.Single(items);
    }

    [Fact]
    public void ReordersItemsWithoutChangingTheirContents()
    {
        var first = new Dictionary<string, object?> { ["title"] = "First" };
        var second = new Dictionary<string, object?> { ["title"] = "Second" };
        var items = new List<Dictionary<string, object?>> { first, second };

        Assert.True(RepeaterValueHelper.TryMove(items, 0, 1));

        Assert.Same(second, items[0]);
        Assert.Same(first, items[1]);
        Assert.False(RepeaterValueHelper.TryMove(items, 1, 1));
    }

    [Fact]
    public void ReadsJsonArrayAndNestedObjectValues()
    {
        using var document = JsonDocument.Parse("""[{"title":"Feature","meta":{"rank":2}}]""");
        var items = RepeaterValueHelper.ReadItems(document.RootElement, Schema);

        Assert.Single(items);
        Assert.Equal("Feature", ((JsonElement)items[0]["title"]!).GetString());
        Assert.Equal(2, ((JsonElement)items[0]["meta.rank"]!).GetInt32());
    }
}
