using System.Text.Json.Nodes;

namespace Acjr3.Tests.Common;

public sealed class PayloadPipelineTests
{
    [Theory]
    [InlineData(null, null, null, ExplicitPayloadSource.None)]
    [InlineData("payload.json", null, null, ExplicitPayloadSource.In)]
    [InlineData(null, "{\"k\":1}", null, ExplicitPayloadSource.Body)]
    [InlineData(null, null, "payload.json", ExplicitPayloadSource.BodyFile)]
    public void TryResolveExplicitPayloadSource_ResolvesExpectedSource(
        string? inPath,
        string? body,
        string? bodyFile,
        ExplicitPayloadSource expected)
    {
        var ok = InputResolver.TryResolveExplicitPayloadSource(inPath, body, bodyFile, out var source, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal(expected, source);
    }

    [Fact]
    public void TryResolveExplicitPayloadSource_RejectsMultipleSources()
    {
        var ok = InputResolver.TryResolveExplicitPayloadSource("payload.json", "{\"x\":1}", null, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Use exactly one explicit payload source: --in, --body, or --body-file.", error);
    }

    [Theory]
    [InlineData("{\"fields\":{\"issuetype\":{\"name\":\"Task\"}}}", "fields")]
    [InlineData("{\"fields\":{}}", "fields")]
    [InlineData("{\"transition\":{},\"fields\":{},\"update\":{}}", "transition")]
    [InlineData("{\"body\":{}}", "body")]
    [InlineData("{\"type\":{},\"inwardIssue\":{},\"outwardIssue\":{}}", "type")]
    [InlineData("{}", null)]
    public void ParseDefaultPayload_ParsesExpectedTemplates(string json, string? expectedTopLevelKey)
    {
        var payload = JsonPayloadPipeline.ParseDefaultPayload(json);

        Assert.NotNull(payload);
        if (expectedTopLevelKey is not null)
        {
            Assert.True(payload.ContainsKey(expectedTopLevelKey));
        }
    }

    [Fact]
    public void SetString_LastWriteWinsForSamePath()
    {
        var payload = new JsonObject();
        JsonPayloadPipeline.SetString(payload, "first", "fields", "summary");
        JsonPayloadPipeline.SetString(payload, "second", "fields", "summary");

        Assert.Equal("second", JsonPayloadPipeline.TryGetString(payload, "fields", "summary"));
    }

    [Fact]
    public void TryParseJsonObject_RejectsNonObjectJson()
    {
        var ok = JsonPayloadPipeline.TryParseJsonObject("[1,2,3]", "--body", out _, out var error);

        Assert.False(ok);
        Assert.Equal("--body payload must be a JSON object.", error);
    }
}
