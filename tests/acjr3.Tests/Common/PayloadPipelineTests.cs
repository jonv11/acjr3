using System.Text.Json.Nodes;

namespace Acjr3.Tests.Common;

public sealed class PayloadPipelineTests
{
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
        var ok = JsonPayloadPipeline.TryParseJsonObject("[1,2,3]", "--in", out _, out var error);

        Assert.False(ok);
        Assert.Equal("--in payload must be a JSON object.", error);
    }
}
