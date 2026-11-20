using System.Text.Json;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class ElementorPreviewDocumentBuilderTests
{
    [Fact]
    public void BuildsSectionColumnAndRequestedWidgetWithNestedSettings()
    {
        var settings = new Dictionary<string, object?>
        {
            ["title"] = "Preview heading",
            ["layout.columns"] = 3
        };

        var elements = ElementorPreviewDocumentBuilder.Build("lyra-section-heading", settings);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(elements));

        var section = document.RootElement[0];
        var column = section.GetProperty("elements")[0];
        var widget = column.GetProperty("elements")[0];

        Assert.Equal("section", section.GetProperty("elType").GetString());
        Assert.Equal("column", column.GetProperty("elType").GetString());
        Assert.Equal("widget", widget.GetProperty("elType").GetString());
        Assert.Equal("lyra-section-heading", widget.GetProperty("widgetType").GetString());
        Assert.Equal("Preview heading", widget.GetProperty("settings").GetProperty("title").GetString());
        Assert.Equal(3, widget.GetProperty("settings").GetProperty("layout").GetProperty("columns").GetInt32());
    }

    [Fact]
    public void RejectsEmptyWidgetType()
    {
        Assert.Throws<ArgumentException>(() =>
            ElementorPreviewDocumentBuilder.Build(" ", new Dictionary<string, object?>()));
    }

    [Fact]
    public void CreatesDistinctElementIdentifiers()
    {
        var first = ElementorPreviewDocumentBuilder.Build("lyra-section-heading", new Dictionary<string, object?>());
        var second = ElementorPreviewDocumentBuilder.Build("lyra-section-heading", new Dictionary<string, object?>());

        using var firstJson = JsonDocument.Parse(JsonSerializer.Serialize(first));
        using var secondJson = JsonDocument.Parse(JsonSerializer.Serialize(second));

        var firstId = firstJson.RootElement[0].GetProperty("id").GetString();
        var secondId = secondJson.RootElement[0].GetProperty("id").GetString();
        Assert.NotEqual(firstId, secondId);
    }
}
