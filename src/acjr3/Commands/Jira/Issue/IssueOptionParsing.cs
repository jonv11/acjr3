using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static partial class IssueCommands
{
    private static bool WasOptionSupplied(ParseResult parseResult, string alias)
    {
        return parseResult.CommandResult
            .Children
            .OfType<System.CommandLine.Parsing.OptionResult>()
            .Any(option => option.Option.HasAlias(alias) && option.Tokens.Count > 0);
    }

    private static bool TryParseDescriptionFormat(string raw, InvocationContext context, out DescriptionFileFormat format)
    {
        format = DescriptionFileFormat.Text;
        if (raw.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (raw.Equals("adf", StringComparison.OrdinalIgnoreCase))
        {
            format = DescriptionFileFormat.Adf;
            return true;
        }

        CliOutput.WriteValidationError(context, "--description-format must be one of: text, adf.");
        return false;
    }

    private static bool TryParseFieldFormat(string raw, InvocationContext context, out FieldFileFormat format)
    {
        format = FieldFileFormat.Json;
        if (raw.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (raw.Equals("adf", StringComparison.OrdinalIgnoreCase))
        {
            format = FieldFileFormat.Adf;
            return true;
        }

        CliOutput.WriteValidationError(context, "--field-format must be one of: json, adf.");
        return false;
    }

    private static bool TryParseBooleanOption(string? raw, string optionName, InvocationContext context, out bool? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!bool.TryParse(raw, out var parsed))
        {
            CliOutput.WriteValidationError(context, $"{optionName} must be 'true' or 'false'.");
            return false;
        }

        value = parsed;
        return true;
    }
}

