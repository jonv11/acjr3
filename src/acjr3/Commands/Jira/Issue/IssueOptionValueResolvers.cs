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
        var effectiveFormat = descriptionFormat ?? "text";

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

        if (parsedFormat == DescriptionFileFormat.Text)
        {
            try
            {
                descriptionValue = TextFileInput.ReadAllTextNormalized(effectiveFile);
                return true;
            }
            catch (Exception ex)
            {
                CliOutput.WriteValidationError(context, $"Failed to read --description-file '{effectiveFile}': {ex.Message}");
                return false;
            }
        }

        if (!TryReadJsonObjectFile(effectiveFile, "--description-file", context, out var descriptionAdf))
        {
            return false;
        }

        descriptionValue = descriptionAdf;
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
        var effectiveFormat = fieldFormat ?? "json";

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

        if (parsedFormat == FieldFileFormat.Adf && parsedFieldValue.ValueKind != JsonValueKind.Object)
        {
            CliOutput.WriteValidationError(context, $"--field-file '{effectiveFile}' must contain a JSON object when --field-format adf is used.");
            return false;
        }

        fieldValue = parsedFieldValue;
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

    private static bool TryReadJsonObjectFile(string filePath, string optionName, InvocationContext context, out JsonElement jsonObject)
    {
        jsonObject = default;
        if (!TryReadJsonFile(filePath, optionName, context, out var parsed))
        {
            return false;
        }

        if (parsed.ValueKind != JsonValueKind.Object)
        {
            CliOutput.WriteValidationError(context, $"{optionName} file '{filePath}' must contain a JSON object.");
            return false;
        }

        jsonObject = parsed;
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

    private static bool TryParseJsonElement(string json, string optionName, InvocationContext context, out JsonElement value)
    {
        value = default;
        try
        {
            using var doc = JsonDocument.Parse(json);
            value = doc.RootElement.Clone();
            return true;
        }
        catch (Exception ex)
        {
            CliOutput.WriteValidationError(context, $"Failed to parse {optionName} payload: {ex.Message}");
            return false;
        }

    }

    private static bool TryNormalizeIssueInputPayload(string? payload, InputFormat format, InvocationContext context, out string? normalizedBody)
    {
        normalizedBody = payload;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return true;
        }

        if (format == InputFormat.Adf)
        {
            if (!TryParseJsonElement(payload, "--in", context, out var adfValue))
            {
                return false;
            }

            normalizedBody = JsonSerializer.Serialize(adfValue);
            return true;
        }

        return true;
    }

    private static bool TryNormalizeTransitionInputPayload(string? payload, InputFormat format, InvocationContext context, out string? normalizedBody)
    {
        normalizedBody = payload;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return true;
        }

        if (format == InputFormat.Markdown || format == InputFormat.Text)
        {
            CliOutput.WriteValidationError(context, "--input-format for issue transition --in must be json or adf.");
            return false;
        }

        return true;
    }
}
