using System.Text.Json;
using Acjr3.Output;

namespace Acjr3.Tests.Output;

public sealed class OutputRendererTests
{
    private readonly OutputRenderer renderer = new();

    [Fact]
    public void RenderEnvelope_JsonlArray_EmitsOneEnvelopePerItem()
    {
        var data = JsonSerializer.SerializeToElement(new[]
        {
            new { id = 1 },
            new { id = 2 }
        });

        var envelope = new CliEnvelope(true, data, null, new CliMeta("1.0", null, null, 200, "GET", "/x"));
        var output = renderer.RenderEnvelope(envelope, OutputPreferences.Default with { Format = OutputFormat.Jsonl });
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("\"id\":1", lines[0]);
        Assert.Contains("\"id\":2", lines[1]);
    }

    [Fact]
    public void RenderText_PlainArray_JoinsScalarsByLine()
    {
        var data = JsonSerializer.SerializeToElement(new[] { "a", "b", "c" });
        var envelope = new CliEnvelope(true, data, null, new CliMeta("1.0", null, null, 200, "GET", "/x"));

        var output = renderer.RenderText(envelope, OutputPreferences.Default with { Format = OutputFormat.Text, Plain = true });

        Assert.Equal($"a{Environment.NewLine}b{Environment.NewLine}c", output);
    }

    [Fact]
    public void RenderEnvelope_AppliesFilterSortAndLimit()
    {
        var data = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "beta", score = 2 },
            new { name = "alpha", score = 1 },
            new { name = "alpha", score = 3 }
        });

        var envelope = new CliEnvelope(true, data, null, new CliMeta("1.0", null, null, 200, "GET", "/x"));
        var prefs = OutputPreferences.Default with { Filter = "name=alpha", Sort = "score:desc", Limit = 1 };

        var output = renderer.RenderEnvelope(envelope, prefs);

        Assert.Contains("\"score\": 3", output);
        Assert.DoesNotContain("\"score\": 1", output);
    }
}
