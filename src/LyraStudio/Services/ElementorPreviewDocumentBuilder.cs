using System.Text.Json;

namespace LyraStudio.Services;

/// <summary>
/// Builds a minimal legacy Section/Column/Widget Elementor document tree for a single widget.
/// The widget ID must be an actual registered Elementor widget type reported by the bridge.
/// </summary>
public static class ElementorPreviewDocumentBuilder
{
    public static IReadOnlyList<object> Build(string widgetType, IDictionary<string, object?> settings)
    {
        if (string.IsNullOrWhiteSpace(widgetType))
            throw new ArgumentException("An Elementor widget type is required.", nameof(widgetType));

        using var settingsDocument = JsonDocument.Parse(SettingsJsonSerializer.Serialize(settings));
        var widget = new Dictionary<string, object?>
        {
            ["id"] = CreateElementId(),
            ["elType"] = "widget",
            ["widgetType"] = widgetType,
            ["settings"] = settingsDocument.RootElement.Clone(),
            ["elements"] = Array.Empty<object>()
        };

        var column = new Dictionary<string, object?>
        {
            ["id"] = CreateElementId(),
            ["elType"] = "column",
            ["settings"] = new Dictionary<string, object?> { ["_column_size"] = 100 },
            ["elements"] = new object[] { widget }
        };

        var section = new Dictionary<string, object?>
        {
            ["id"] = CreateElementId(),
            ["elType"] = "section",
            ["settings"] = new Dictionary<string, object?>(),
            ["elements"] = new object[] { column }
        };

        return new object[] { section };
    }

    private static string CreateElementId() => Guid.NewGuid().ToString("N")[..8];
}
