using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class PreviewBridgeClientTests
{
    [Theory]
    [InlineData("https://example.test/wp-json/", "https://example.test/wp-json/lyra-studio/v1/capabilities")]
    [InlineData("https://example.test/shop/wp-json/", "https://example.test/shop/wp-json/lyra-studio/v1/capabilities")]
    [InlineData("https://example.test/shop/wp-json", "https://example.test/shop/wp-json/lyra-studio/v1/capabilities")]
    public void BuildsPrettyPermalinkBridgeEndpoint(string restRoot, string expected)
    {
        var result = PreviewBridgeClient.BuildEndpointUri(new Uri(restRoot), "capabilities");

        Assert.Equal(expected, result.ToString());
    }

    [Fact]
    public void PreservesRestRouteQueryWhenPrettyPermalinksAreUnavailable()
    {
        var result = PreviewBridgeClient.BuildEndpointUri(
            new Uri("https://example.test/?rest_route=%2Fwp%2Fv2"),
            "previews/abc");

        Assert.Equal("example.test", result.Host);
        Assert.Contains("rest_route=", result.Query);
        Assert.Contains("lyra-studio", result.Query);
        Assert.Contains("previews", result.Query);
    }

    [Fact]
    public void RejectsRelativeRestApiRoot()
    {
        Assert.Throws<ArgumentException>(() =>
            PreviewBridgeClient.BuildEndpointUri(new Uri("/wp-json/", UriKind.Relative), "capabilities"));
    }
}
