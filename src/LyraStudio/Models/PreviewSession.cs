namespace LyraStudio.Models;

public sealed record PreviewRequest(
    string WidgetId,
    IReadOnlyDictionary<string, object?> Settings,
    string Device = "desktop");

public sealed record PreviewSession(
    string SessionId,
    Uri PreviewUri,
    DateTimeOffset ExpiresAt,
    string? RenderVersion = null);
