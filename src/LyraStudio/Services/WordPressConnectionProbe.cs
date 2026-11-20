using System.Net.Http;
using System.Text.RegularExpressions;
using LyraStudio.Models;

namespace LyraStudio.Services;

/// <summary>
/// Discovers and reads the public WordPress REST API index. All requests are GET/HEAD;
/// no posts, pages, Elementor documents, or plugin data are changed.
/// </summary>
public sealed class WordPressConnectionProbe
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly Regex RestApiLinkPattern = new(
        @"<(?<url>[^>]+)>\s*;\s*rel\s*=\s*[""']https://api\.w\.org/[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<WordPressSiteCapabilities> CheckAsync(
        string siteUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri) ||
            (siteUri.Scheme != Uri.UriSchemeHttps && siteUri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Enter a valid http:// or https:// WordPress site URL.");

        var restApiRoot = await DiscoverRestApiRootAsync(siteUri, cancellationToken);
        using var response = await Client.GetAsync(restApiRoot, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"WordPress REST API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return WordPressRestIndexParser.Parse(json, response.RequestMessage?.RequestUri ?? restApiRoot);
    }

    private static async Task<Uri> DiscoverRestApiRootAsync(Uri siteUri, CancellationToken cancellationToken)
    {
        // WordPress recommends discovering the REST root from the Link header.
        // This also supports installations whose REST API is not at /wp-json/.
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, siteUri);
            using var response = await Client.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Link", out var values))
            {
                foreach (var value in values)
                {
                    var match = RestApiLinkPattern.Match(value);
                    if (match.Success &&
                        Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var discovered))
                        return discovered;
                }
            }
        }
        catch (HttpRequestException)
        {
            // Some hosts block HEAD requests. Fall back to the conventional REST path.
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A slow/blocked HEAD must not prevent the conventional read-only probe.
        }

        return BuildFallbackRestApiRoot(siteUri);
    }

    public static Uri BuildFallbackRestApiRoot(Uri siteUri)
    {
        var builder = new UriBuilder(siteUri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };

        var path = builder.Path.TrimEnd('/');
        if (path.EndsWith("/wp-json", StringComparison.OrdinalIgnoreCase))
            builder.Path = path + "/";
        else
            builder.Path = (path.Length == 0 ? string.Empty : path) + "/wp-json/";

        return builder.Uri;
    }
}
