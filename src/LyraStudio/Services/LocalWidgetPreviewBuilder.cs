using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Renders a provider-supplied static template without WordPress, PHP,
/// network calls, or provider JavaScript. Template values are HTML encoded.
/// </summary>
public sealed class LocalWidgetPreviewBuilder
{
    private static readonly Regex TokenPattern = new(
        @"\{\{\s*([a-zA-Z0-9_.-]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string BuildDocument(
        WidgetDescriptor widget,
        IReadOnlyDictionary<string, object?> settings,
        int viewportWidth)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(settings);

        if (widget.Preview is null || string.IsNullOrWhiteSpace(widget.Preview.Html))
            return BuildMissingTemplateDocument(widget);

        var settingsDictionary = settings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        using var settingsDocument = JsonDocument.Parse(SettingsJsonSerializer.Serialize(settingsDictionary));

        var html = TokenPattern.Replace(widget.Preview.Html, match =>
        {
            var key = match.Groups[1].Value;
            if (string.Equals(key, "id", StringComparison.OrdinalIgnoreCase))
                return WebUtility.HtmlEncode(widget.Id);
            if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                return WebUtility.HtmlEncode(widget.Name);

            return WebUtility.HtmlEncode(ResolveValue(settingsDocument.RootElement, key));
        });

        var css = widget.Preview.Css ?? "";
        var width = Math.Clamp(viewportWidth, 320, 1920);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:;">
              <title>{{WebUtility.HtmlEncode(widget.Name)}} · Local Preview</title>
              <style>
                * { box-sizing: border-box; }
                html, body { margin: 0; min-height: 100%; background: #f4f6fa; color: #202432; font-family: "Segoe UI", Arial, sans-serif; }
                body { padding: 24px; }
                .lyra-preview-root { width: 100%; min-height: 160px; }
                {{css}}
              </style>
            </head>
            <body><main class="lyra-preview-root" data-viewport-width="{{width}}">{{html}}</main></body>
            </html>
            """;
    }

    private static string ResolveValue(JsonElement root, string dottedPath)
    {
        var current = root;
        foreach (var segment in dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var property))
            {
                current = property;
                continue;
            }

            if (current.ValueKind == JsonValueKind.Array &&
                int.TryParse(segment, out var index) &&
                index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
                continue;
            }

            return "";
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString() ?? "",
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object or JsonValueKind.Array => current.GetRawText(),
            _ => current.ToString()
        };
    }

    private static string BuildMissingTemplateDocument(WidgetDescriptor widget)
    {
        var safeName = WebUtility.HtmlEncode(widget.Name);
        var safeId = WebUtility.HtmlEncode(widget.Id);
        return $$"""
            <!doctype html>
            <html lang="en"><head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline';">
              <style>
                * { box-sizing: border-box; }
                body { margin:0; padding:32px; background:#f4f6fa; color:#202432; font:14px "Segoe UI",Arial,sans-serif; }
                .notice { width:min(100%, 680px); margin:40px auto; padding:24px; background:white; border:1px solid #e4e7ec; border-radius:14px; }
                .muted { color:#667085; line-height:1.65; }
                code { color:#7567e8; }
              </style>
            </head><body><section class="notice">
              <h2>{{safeName}}</h2>
              <p class="muted">This provider has not supplied a local preview template for this widget yet.</p>
              <p class="muted">Widget ID: <code>{{safeId}}</code></p>
              <p class="muted">The settings editor is running locally. Add a static HTML/CSS preview template to the provider manifest to render this widget here.</p>
            </section></body></html>
            """;
    }
}
