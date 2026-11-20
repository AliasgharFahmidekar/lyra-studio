using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class WordPressRestIndexParserTests
{
    private static readonly Uri RestRoot = new("https://example.test/wp-json/");

    [Fact]
    public void DiscoversWordPressPagesElementorAndLyraNamespaces()
    {
        const string json = """
        {
          "namespaces": ["oembed/1.0", "wp/v2", "elementor/v1", "lyra-core/v1"],
          "routes": {
            "/wp/v2/pages": {},
            "/wp/v2/pages/(?P<id>[\\d]+)": {},
            "/elementor/v1/documents/(?P<id>[\\d]+)/media/import": {},
            "/lyra-core/v1/manifest": {}
          }
        }
        """;

        var result = WordPressRestIndexParser.Parse(json, RestRoot);

        Assert.True(result.HasWordPressPagesRoute);
        Assert.True(result.HasElementorNamespace);
        Assert.True(result.HasLyraNamespace);
        Assert.Single(result.ElementorRoutes);
        Assert.Equal(4, result.DiscoveredRouteCount);
    }

    [Fact]
    public void DoesNotClaimPreviewSupportFromNamespacesAlone()
    {
        const string json = """
        {
          "namespaces": ["wp/v2", "elementor/v1"],
          "routes": { "/wp/v2/pages": {} }
        }
        """;

        var result = WordPressRestIndexParser.Parse(json, RestRoot);

        Assert.True(result.HasElementorNamespace);
        Assert.Empty(result.ElementorRoutes);
        Assert.Contains("Live preview capability: not established", result.ToDisplayText());
    }

    [Fact]
    public void HandlesSitesWithoutElementorOrLyraNamespaces()
    {
        const string json = """
        {
          "namespaces": ["wp/v2"],
          "routes": { "/wp/v2/posts": {}, "/wp/v2/pages": {} }
        }
        """;

        var result = WordPressRestIndexParser.Parse(json, RestRoot);

        Assert.True(result.HasWordPressPagesRoute);
        Assert.False(result.HasElementorNamespace);
        Assert.False(result.HasLyraNamespace);
    }

    [Theory]
    [InlineData("https://example.test", "https://example.test/wp-json/")]
    [InlineData("https://example.test/shop", "https://example.test/shop/wp-json/")]
    [InlineData("https://example.test/shop/wp-json", "https://example.test/shop/wp-json/")]
    [InlineData("https://example.test/shop/?preview=1", "https://example.test/shop/wp-json/")]
    public void BuildsFallbackRestRootAndPreservesSubdirectory(string siteUrl, string expected)
    {
        var result = WordPressConnectionProbe.BuildFallbackRestApiRoot(new Uri(siteUrl));

        Assert.Equal(expected, result.ToString());
    }

    [Fact]
    public void DiscoversRestRootFromWordPressLinkHeader()
    {
        var result = WordPressConnectionProbe.TryParseRestApiLinkHeader(
        [
            "<https://example.test/wp-json/>; rel=\"https://api.w.org/\"",
            "<https://example.test/wp-json/wp/v2/pages>; rel=\"alternate\""
        ]);

        Assert.Equal("https://example.test/wp-json/", result?.ToString());
    }

    [Fact]
    public void IgnoresLinkHeadersWithoutWordPressRestRelation()
    {
        var result = WordPressConnectionProbe.TryParseRestApiLinkHeader(
        [
            "<https://example.test/wp-json/>; rel=\"alternate\"",
            "<https://example.test/feed/>; rel=\"https://api.w.org/feed\""
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void RejectsMalformedRestIndex()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            WordPressRestIndexParser.Parse("{", RestRoot));
    }
}
