using LyraStudio.Models;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class LocalWidgetPreviewBuilderTests
{
    private readonly LocalWidgetPreviewBuilder _builder = new();

    [Fact]
    public void RendersTemplateAndExpandsNestedSettings()
    {
        var widget = new WidgetDescriptor(
            "sample.card",
            "Sample card",
            Preview: new WidgetPreviewDefinition
            {
                Html = "<article><h1>{{title}}</h1><p>{{layout.subtitle}}</p><small>{{name}}</small></article>",
                Css = "article { padding: 20px; }"
            });
        var settings = new Dictionary<string, object?>
        {
            ["title"] = "Local title",
            ["layout.subtitle"] = "Nested subtitle"
        };

        var html = _builder.BuildDocument(widget, settings, 1280);

        Assert.Contains("<h1>Local title</h1>", html);
        Assert.Contains("<p>Nested subtitle</p>", html);
        Assert.Contains("<small>Sample card</small>", html);
        Assert.Contains("article { padding: 20px; }", html);
        Assert.Contains("width=device-width", html);
    }

    [Fact]
    public void HtmlEncodesSettingValuesBeforeInsertingThem()
    {
        var widget = new WidgetDescriptor("sample.card", "Sample card",
            Preview: new WidgetPreviewDefinition { Html = "<div>{{title}}</div>" });
        var html = _builder.BuildDocument(widget,
            new Dictionary<string, object?> { ["title"] = "<script>alert(1)</script>" }, 390);

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }

    [Fact]
    public void ShowsExplicitMessageWhenProviderHasNoLocalTemplate()
    {
        var widget = new WidgetDescriptor("sample.card", "Sample card");
        var html = _builder.BuildDocument(widget, new Dictionary<string, object?>(), 768);

        Assert.Contains("has not supplied a local preview template", html);
        Assert.Contains("sample.card", html);
    }
}
