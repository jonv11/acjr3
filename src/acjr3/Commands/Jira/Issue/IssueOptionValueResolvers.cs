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
    private static bool TryResolveDescriptionValue(
        string? descriptionInline,
        string? descriptionFile,
        string? descriptionFormat,
        bool formatOptionSpecified,
        InvocationContext context,
        out object? descriptionValue)
    {
        descriptionValue = descriptionInline;

        var effectiveFile = descriptionFile;
        var effectiveFormat = descriptionFormat ?? "adf";

        if (string.IsNullOrWhiteSpace(effectiveFile))
        {
            if (formatOptionSpecified)
            {
                CliOutput.WriteValidationError(context, "--description-format requires --description-file.");
                return false;
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(descriptionInline))
        {
            CliOutput.WriteValidationError(context, "Use either --description or --description-file, not both.");
            return false;
        }

        if (!TryParseDescriptionFormat(effectiveFormat, context, out var parsedFormat))
        {
            return false;
        }

        if (!TryReadJsonFile(effectiveFile!, "--description-file", context, out var parsedDescriptionValue))
        {
            return false;
        }

        if (parsedFormat == DescriptionFileFormat.Adf
            && !TryValidateAdfDocument(
                parsedDescriptionValue,
                "--description-file",
                effectiveFile!,
                "--description-format",
                context))
        {
            return false;
        }

        descriptionValue = parsedDescriptionValue;
        return true;
    }

    private static bool TryResolveNamedFieldFileValue(
        string? fieldName,
        string? fieldFile,
        string? fieldFormat,
        bool formatOptionSpecified,
        string fieldNameOptionName,
        InvocationContext context,
        out string? resolvedName,
        out JsonElement? fieldValue)
    {
        resolvedName = null;
        fieldValue = null;

        var effectiveFile = fieldFile;
        var effectiveFormat = fieldFormat ?? "adf";

        var hasName = !string.IsNullOrWhiteSpace(fieldName);
        var hasFile = !string.IsNullOrWhiteSpace(effectiveFile);
        if (!hasName && !hasFile)
        {
            if (formatOptionSpecified)
            {
                CliOutput.WriteValidationError(context, "--field-format requires --field-file.");
                return false;
            }

            return true;
        }

        if (!hasName || !hasFile)
        {
            CliOutput.WriteValidationError(context, $"Use {fieldNameOptionName} together with --field-file.");
            return false;
        }

        if (!TryParseFieldFormat(effectiveFormat, context, out var parsedFormat))
        {
            return false;
        }

        resolvedName = fieldName!.Trim();

        if (!TryReadJsonFile(effectiveFile!, "--field-file", context, out var parsedFieldValue))
        {
            return false;
        }

        if (parsedFormat == FieldFileFormat.Adf
            && !TryValidateAdfDocument(
                parsedFieldValue,
                "--field-file",
                effectiveFile!,
                "--field-format",
                context))
        {
            return false;
        }

        fieldValue = parsedFieldValue;
        return true;
    }

    private static bool TryResolveCommentBodyValue(
        string? textInline,
        string? textFile,
        string? textFormat,
        bool formatOptionSpecified,
        bool textOptionSpecified,
        InvocationContext context,
        out JsonNode? commentBodyValue)
    {
        commentBodyValue = null;

        var effectiveFile = textFile;
        var effectiveFormat = textFormat ?? "adf";
        var hasTextFile = !string.IsNullOrWhiteSpace(effectiveFile);

        if (!hasTextFile && !textOptionSpecified)
        {
            if (formatOptionSpecified)
            {
                CliOutput.WriteValidationError(context, "--text-format requires --text-file.");
                return false;
            }

            return true;
        }

        if (hasTextFile && textOptionSpecified)
        {
            CliOutput.WriteValidationError(context, "Use either --text or --text-file, not both.");
            return false;
        }

        if (!hasTextFile)
        {
            if (!string.IsNullOrWhiteSpace(textInline))
            {
                commentBodyValue = BuildCommentAdfTextNode(textInline);
            }

            return true;
        }

        if (!TryParseCommentTextFormat(effectiveFormat, context, out var parsedFormat))
        {
            return false;
        }

        if (!TryReadJsonFile(effectiveFile!, "--text-file", context, out var parsedCommentValue))
        {
            return false;
        }

        if (parsedFormat == CommentTextFileFormat.Adf
            && !TryValidateAdfDocument(
                parsedCommentValue,
                "--text-file",
                effectiveFile!,
                "--text-format",
                context))
        {
            return false;
        }

        commentBodyValue = JsonNode.Parse(parsedCommentValue.GetRawText());
        return true;
    }

    private static bool TryResolveProjectKey(string? projectOption, string? projectArgument, InvocationContext context, out string? project)
    {
        project = projectOption;
        if (string.IsNullOrWhiteSpace(project))
        {
            project = projectArgument;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(projectArgument)
            && !project.Equals(projectArgument, StringComparison.OrdinalIgnoreCase))
        {
            CliOutput.WriteValidationError(context, $"Project mismatch: argument '{projectArgument}' does not match --project '{projectOption}'.");
            return false;
        }

        return true;
    }

    private static bool TryReadJsonFile(string filePath, string optionName, InvocationContext context, out JsonElement jsonValue)
    {
        jsonValue = default;
        try
        {
            var text = TextFileInput.ReadAllTextNormalized(filePath);
            using var doc = JsonDocument.Parse(text);
            jsonValue = doc.RootElement.Clone();
            return true;
        }
        catch (Exception ex)
        {
            CliOutput.WriteValidationError(context, $"Failed to read {optionName} file '{filePath}': {ex.Message}");
            return false;
        }
    }

    private static bool TryValidateAdfDocument(
        JsonElement value,
        string fileOptionName,
        string filePath,
        string formatOptionName,
        InvocationContext context)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            CliOutput.WriteValidationError(
                context,
                $"{fileOptionName} '{filePath}' must contain an ADF document object (type='doc', numeric version, array content) when {formatOptionName} adf is used.");
            return false;
        }

        if (!value.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || !string.Equals(typeElement.GetString(), "doc", StringComparison.Ordinal))
        {
            CliOutput.WriteValidationError(
                context,
                $"{fileOptionName} '{filePath}' must contain an ADF document object (type='doc', numeric version, array content) when {formatOptionName} adf is used.");
            return false;
        }

        if (!value.TryGetProperty("version", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number)
        {
            CliOutput.WriteValidationError(
                context,
                $"{fileOptionName} '{filePath}' must contain an ADF document object (type='doc', numeric version, array content) when {formatOptionName} adf is used.");
            return false;
        }

        if (!value.TryGetProperty("content", out var contentElement)
            || contentElement.ValueKind != JsonValueKind.Array)
        {
            CliOutput.WriteValidationError(
                context,
                $"{fileOptionName} '{filePath}' must contain an ADF document object (type='doc', numeric version, array content) when {formatOptionName} adf is used.");
            return false;
        }

        return true;
    }

}
