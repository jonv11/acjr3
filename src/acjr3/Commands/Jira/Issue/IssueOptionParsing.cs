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
        format = DescriptionFileFormat.Json;
        if (!InputResolver.TryParseFormat(raw, "--description-format", out var inputFormat, out var error))
        {
            CliOutput.WriteValidationError(context, error);
            return false;
        }

        if (inputFormat == InputFormat.Adf)
        {
            format = DescriptionFileFormat.Adf;
        }

        return true;
    }

    private static bool TryParseFieldFormat(string raw, InvocationContext context, out FieldFileFormat format)
    {
        format = FieldFileFormat.Json;
        if (!InputResolver.TryParseFormat(raw, "--field-format", out var inputFormat, out var error))
        {
            CliOutput.WriteValidationError(context, error);
            return false;
        }

        if (inputFormat == InputFormat.Adf)
        {
            format = FieldFileFormat.Adf;
        }

        return true;
    }

    private static bool TryParseCommentTextFormat(string raw, InvocationContext context, out CommentTextFileFormat format)
    {
        format = CommentTextFileFormat.Json;
        if (!InputResolver.TryParseFormat(raw, "--text-format", out var inputFormat, out var error))
        {
            CliOutput.WriteValidationError(context, error);
            return false;
        }

        if (inputFormat == InputFormat.Adf)
        {
            format = CommentTextFileFormat.Adf;
        }

        return true;
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

