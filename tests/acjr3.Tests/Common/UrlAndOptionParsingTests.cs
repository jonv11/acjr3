using Acjr3;

namespace Acjr3.Tests.Common;

public sealed class UrlAndOptionParsingTests
{
    [Fact]
    public void NormalizePath_AddsLeadingSlash()
    {
        var path = UrlBuilder.NormalizePath("rest/api/3/myself");
        Assert.Equal("/rest/api/3/myself", path);
    }

    [Fact]
    public void Build_AddsQueryParameters()
    {
        var uri = UrlBuilder.Build(
            new Uri("https://example.atlassian.net"),
            "/rest/api/3/search",
            [new KeyValuePair<string, string>("jql", "project = TEST")]);

        Assert.Contains("jql=project%20%3D%20TEST", uri.Query);
    }

    [Fact]
    public void ParsePairs_ParsesValidKeyValue()
    {
        var ok = RequestOptionParser.TryParsePairs(["a=1", "b=2"], out var pairs, out var error);

        Assert.True(ok, error);
        Assert.Equal(2, pairs.Count);
        Assert.Equal("a", pairs[0].Key);
        Assert.Equal("1", pairs[0].Value);
    }

    [Fact]
    public void ParsePairs_FailsOnInvalidItem()
    {
        var ok = RequestOptionParser.TryParsePairs(["invalid"], out _, out var error);

        Assert.False(ok);
        Assert.Contains("Invalid key=value", error);
    }
}


