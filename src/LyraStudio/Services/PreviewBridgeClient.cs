using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Authenticated client for the optional Lyra Studio Preview Bridge.
/// All content writes are addressed by the server-issued temporary session UUID.
/// </summary>
public sealed class PreviewBridgeClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<PreviewBridgeCapabilities> GetCapabilitiesAsync(
        Uri restApiRoot, string username, string applicationPassword,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get, BuildEndpointUri(restApiRoot, "capabilities"),
            username, applicationPassword, null, cancellationToken);
        return await ReadResponseAsync<PreviewBridgeCapabilities>(response, cancellationToken);
    }

    public async Task<BridgePreviewSession> CreatePreviewAsync(
        Uri restApiRoot, string username, string applicationPassword,
        string widgetType, string widgetTitle, IDictionary<string, object?> settings,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(widgetType, widgetTitle, settings);
        using var response = await SendAsync(
            HttpMethod.Post, BuildEndpointUri(restApiRoot, "previews"),
            username, applicationPassword, payload, cancellationToken);
        var session = await ReadResponseAsync<BridgePreviewSession>(response, cancellationToken);
        ValidateSession(session, restApiRoot);
        return session;
    }

    public async Task<BridgePreviewSession> UpdatePreviewAsync(
        Uri restApiRoot, string username, string applicationPassword,
        string sessionId, string widgetType, string widgetTitle,
        IDictionary<string, object?> settings, CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);
        var payload = BuildPayload(widgetType, widgetTitle, settings);
        using var response = await SendAsync(
            HttpMethod.Post, BuildEndpointUri(restApiRoot, $"previews/{Uri.EscapeDataString(sessionId)}"),
            username, applicationPassword, payload, cancellationToken);
        var session = await ReadResponseAsync<BridgePreviewSession>(response, cancellationToken);
        ValidateSession(session, restApiRoot);
        return session;
    }

    public async Task DeletePreviewAsync(
        Uri restApiRoot, string username, string applicationPassword,
        string sessionId, CancellationToken cancellationToken = default)
    {
        ValidateSessionId(sessionId);
        using var response = await SendAsync(
            HttpMethod.Delete, BuildEndpointUri(restApiRoot, $"previews/{Uri.EscapeDataString(sessionId)}"),
            username, applicationPassword, null, cancellationToken);
        await ReadResponseAsync<JsonElement>(response, cancellationToken);
    }

    public static Uri BuildEndpointUri(Uri restApiRoot, string endpoint)
    {
        if (!restApiRoot.IsAbsoluteUri ||
            (restApiRoot.Scheme != Uri.UriSchemeHttps && restApiRoot.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("A valid WordPress REST API root is required.", nameof(restApiRoot));

        var route = "/lyra-studio/v1/" + endpoint.TrimStart('/');
        var builder = new UriBuilder(restApiRoot);

        if (builder.Query.Contains("rest_route=", StringComparison.OrdinalIgnoreCase))
        {
            var queryParts = builder.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !part.StartsWith("rest_route=", StringComparison.OrdinalIgnoreCase))
                .ToList();
            queryParts.Add("rest_route=" + Uri.EscapeDataString(route));
            builder.Query = string.Join("&", queryParts);
            builder.Fragment = string.Empty;
            return builder.Uri;
        }

        builder.Query = string.Empty;
        builder.Fragment = string.Empty;
        var baseUri = builder.Uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? builder.Uri
            : new Uri(builder.Uri.AbsoluteUri + "/");
        return new Uri(baseUri, "lyra-studio/v1/" + endpoint.TrimStart('/'));
    }

    private static object BuildPayload(
        string widgetType, string title, IDictionary<string, object?> settings)
    {
        return new Dictionary<string, object?>
        {
            ["title"] = string.IsNullOrWhiteSpace(title) ? "Lyra Studio widget preview" : title,
            ["ttl_seconds"] = 1800,
            ["settings"] = new Dictionary<string, object?>(),
            ["elements"] = ElementorPreviewDocumentBuilder.Build(widgetType, settings)
        };
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, Uri endpoint, string username, string applicationPassword,
        object? payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(applicationPassword))
            throw new InvalidOperationException("Enter a WordPress username and a dedicated Application Password to use Preview Bridge.");
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Preview Bridge sends WordPress credentials only over HTTPS. Enable HTTPS on the connected WordPress site.");

        using var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var password = applicationPassword.Replace(" ", string.Empty, StringComparison.Ordinal);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);

        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = $"Preview Bridge returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
            try
            {
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.TryGetProperty("message", out var element) &&
                    element.ValueKind == JsonValueKind.String)
                    message = element.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new BridgeApiException((int)response.StatusCode, message);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new InvalidDataException("Preview Bridge returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Preview Bridge returned invalid JSON.", ex);
        }
    }

    private static void ValidateSession(BridgePreviewSession session, Uri restApiRoot)
    {
        ValidateSessionId(session.SessionId);
        if (!string.Equals(session.Status, "draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Preview Bridge did not return a draft session. The app refused to open it.");
        var previewUri = session.GetSafePreviewUri();
        if (!string.Equals(previewUri.Authority, restApiRoot.Authority, StringComparison.OrdinalIgnoreCase) ||
            previewUri.Scheme != restApiRoot.Scheme)
            throw new InvalidDataException("The preview URL origin does not match the connected WordPress site.");
    }

    private static void ValidateSessionId(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out _))
            throw new ArgumentException("The Preview Bridge session ID is invalid.", nameof(sessionId));
    }
}
