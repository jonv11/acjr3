using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Acjr3.Output;

namespace Acjr3.Common;

public static class OutputOptionBinding
{
    private const string FormatAlias = "--format";
    private const string PlainAlias = "--plain";
    private const string PrettyAlias = "--pretty";
    private const string CompactAlias = "--compact";
    private const string SelectAlias = "--select";
    private const string FilterAlias = "--filter";
    private const string SortAlias = "--sort";
    private const string LimitAlias = "--limit";
    private const string CursorAlias = "--cursor";
    private const string PageAlias = "--page";
    private const string AllAlias = "--all";

    public static void AddGlobalOptions(Command command)
    {
        command.AddGlobalOption(new Option<string>(FormatAlias, () => "json", "Output format: json|jsonl|text."));
        command.AddGlobalOption(new Option<bool>(PlainAlias, "Render scalar data as plain text."));
        command.AddGlobalOption(new Option<bool>(PrettyAlias, "Pretty-print JSON output."));
        command.AddGlobalOption(new Option<bool>(CompactAlias, "Compact/minified JSON output."));
        command.AddGlobalOption(new Option<string?>(SelectAlias, "Select fields to project (comma-separated paths)."));
        command.AddGlobalOption(new Option<string?>(FilterAlias, "Filter expression (for example, field=value)."));
        command.AddGlobalOption(new Option<string?>(SortAlias, "Sort expression (for example, field:asc or field:desc)."));
        command.AddGlobalOption(new Option<int?>(LimitAlias, "Limit output items."));
        command.AddGlobalOption(new Option<string?>(CursorAlias, "Output cursor token."));
        command.AddGlobalOption(new Option<int?>(PageAlias, "Output page number."));
        command.AddGlobalOption(new Option<bool>(AllAlias, "Emit all pages/items when supported."));
    }

    public static bool TryResolve(ParseResult parseResult, out OutputPreferences preferences, out string error)
    {
        error = string.Empty;
        preferences = OutputPreferences.Default;

        var formatRaw = GetStringOption(parseResult, FormatAlias) ?? "json";
        var formatSpecified = IsOptionSpecified(parseResult, FormatAlias);
        var useTextFormat = formatRaw.Equals("text", StringComparison.OrdinalIgnoreCase);
        var useJsonlFormat = formatRaw.Equals("jsonl", StringComparison.OrdinalIgnoreCase);
        var useJsonFormat = formatRaw.Equals("json", StringComparison.OrdinalIgnoreCase);
        var usePlain = GetBoolOption(parseResult, PlainAlias);
        var usePretty = GetBoolOption(parseResult, PrettyAlias);
        var useCompact = GetBoolOption(parseResult, CompactAlias);
        var select = GetStringOption(parseResult, SelectAlias);
        var filter = GetStringOption(parseResult, FilterAlias);
        var sort = GetStringOption(parseResult, SortAlias);
        var limit = GetIntOption(parseResult, LimitAlias);
        var cursor = GetStringOption(parseResult, CursorAlias);
        var page = GetIntOption(parseResult, PageAlias);
        var all = GetBoolOption(parseResult, AllAlias);

        if (!useJsonFormat && !useJsonlFormat && !useTextFormat)
        {
            error = "--format must be one of: json, jsonl, text.";
            return false;
        }

        if (usePretty && useCompact)
        {
            error = "Use either --pretty or --compact, not both.";
            return false;
        }

        if (useTextFormat && (usePretty || useCompact))
        {
            error = "--format text cannot be combined with --pretty or --compact.";
            return false;
        }

        if (usePlain && formatSpecified && !useTextFormat)
        {
            error = "--plain requires --format text.";
            return false;
        }

        if (limit is < 0)
        {
            error = "--limit must be zero or greater.";
            return false;
        }

        if (page is < 0)
        {
            error = "--page must be zero or greater.";
            return false;
        }

        var format = useTextFormat
            ? OutputFormat.Text
            : (useJsonlFormat ? OutputFormat.Jsonl : OutputFormat.Json);
        var style = useCompact ? JsonStyle.Compact : JsonStyle.Pretty;
        preferences = new OutputPreferences(format, style, select, filter, sort, limit, cursor, page, all, usePlain);
        return true;
    }

    public static bool TryResolveOrReport(ParseResult parseResult, InvocationContext context, out OutputPreferences preferences)
    {
        preferences = OutputPreferences.Default;
        if (!TryResolve(parseResult, out var resolvedPreferences, out var error))
        {
            Console.Error.WriteLine(error);
            context.ExitCode = (int)CliExitCode.Validation;
            return false;
        }

        preferences = resolvedPreferences;
        return true;
    }

    private static bool GetBoolOption(ParseResult parseResult, string alias)
    {
        var result = FindOption(parseResult.CommandResult, alias);
        return result?.GetValueOrDefault<bool>() ?? false;
    }

    private static OptionResult? FindOption(SymbolResult symbolResult, string alias)
    {
        foreach (var child in symbolResult.Children)
        {
            if (child is OptionResult optionResult && optionResult.Option.HasAlias(alias))
            {
                return optionResult;
            }

            var nested = FindOption(child, alias);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string? GetStringOption(ParseResult parseResult, string alias)
    {
        var result = FindOption(parseResult.CommandResult, alias);
        return result?.GetValueOrDefault<string?>();
    }

    private static int? GetIntOption(ParseResult parseResult, string alias)
    {
        var result = FindOption(parseResult.CommandResult, alias);
        return result?.GetValueOrDefault<int?>();
    }

    private static bool IsOptionSpecified(ParseResult parseResult, string alias)
    {
        var result = FindOption(parseResult.CommandResult, alias);
        return result is not null && result.Tokens.Count > 0;
    }
}
