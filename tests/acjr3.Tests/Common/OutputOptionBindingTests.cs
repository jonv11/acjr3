using System.CommandLine;
using System.CommandLine.Parsing;
using Acjr3.Output;

namespace Acjr3.Tests.Common;

public sealed class OutputOptionBindingTests
{
    [Fact]
    public void TryResolve_DefaultsToJsonPretty()
    {
        var parseResult = Parse("issue");
        var ok = OutputOptionBinding.TryResolve(parseResult, out var preferences, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal(OutputFormat.Json, preferences.Format);
        Assert.Equal(JsonStyle.Pretty, preferences.JsonStyle);
    }

    [Fact]
    public void TryResolve_CompactAndPrettyConflict()
    {
        var parseResult = Parse("issue", "--compact", "--pretty");
        var ok = OutputOptionBinding.TryResolve(parseResult, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Use either --pretty or --compact, not both.", error);
    }

    [Fact]
    public void TryResolve_PlainRequiresText()
    {
        var parseResult = Parse("issue", "--plain", "--format", "json");
        var ok = OutputOptionBinding.TryResolve(parseResult, out _, out var error);

        Assert.False(ok);
        Assert.Equal("--plain requires --format text.", error);
    }

    private static ParseResult Parse(params string[] args)
    {
        var root = new RootCommand();
        var issue = new Command("issue");
        OutputOptionBinding.AddGlobalOptions(issue);
        root.AddCommand(issue);
        return root.Parse(args);
    }
}

