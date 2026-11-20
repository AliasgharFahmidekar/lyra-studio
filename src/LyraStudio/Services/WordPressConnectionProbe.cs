using System.Net.Http;

namespace LyraStudio.Services;

/// <summary>
/// Checks only the public WordPress REST API root. This is connectivity detection,
/// not Lyra widget discovery and not a write operation.
/// </summary>
public sealed class WordPressConnectionProbe
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<string> CheckAsync(string siteUrl)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Enter a valid http:// or https:// WordPress site URL.");

        var root = new Uri(uri, "/wp-json/");
        using var response = await Client.GetAsync(root);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"WordPress REST API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        return root.ToString();
    }
}
