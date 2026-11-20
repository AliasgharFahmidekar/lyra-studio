using System.IO;
using System.Text.Json.Serialization;

namespace LyraStudio.Models;

public sealed class PreviewBridgeCapabilities
{
    [JsonPropertyName("bridge_version")]
    public string BridgeVersion { get; init; } = "";

    [JsonPropertyName("wordpress")]
    public string WordPressVersion { get; init; } = "";

    [JsonPropertyName("elementor_ready")]
    public bool ElementorReady { get; init; }

    [JsonPropertyName("elementor")]
    public string? ElementorVersion { get; init; }

    [JsonPropertyName("can_edit_pages")]
    public bool CanEditPages { get; init; }

    [JsonPropertyName("widget_types")]
    public IReadOnlyList<string> WidgetTypes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("preview_mode")]
    public string PreviewMode { get; init; } = "";

    [JsonPropertyName("maximum_ttl")]
    public int MaximumTtlSeconds { get; init; }
}

public sealed class BridgePreviewSession
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = "";

    [JsonPropertyName("post_id")]
    public int PostId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("preview_url")]
    public string PreviewUrl { get; init; } = "";

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("elementor")]
    public string? ElementorVersion { get; init; }

    public Uri GetSafePreviewUri()
    {
        if (!Uri.TryCreate(PreviewUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidDataException("The Preview Bridge returned an invalid preview URL.");
        return uri;
    }
}

public sealed class BridgeApiException : Exception
{
    public int StatusCode { get; }

    public BridgeApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
