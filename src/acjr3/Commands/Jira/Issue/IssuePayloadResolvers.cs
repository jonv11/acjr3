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
    private static async Task<(bool Ok, JsonObject? Payload)> TryResolveIssueJsonBasePayloadAsync(
        string defaultPayload,
        string? inPath,
        string? body,
        string? bodyFile,
        InputFormat inputFormat,
        ExplicitPayloadSource payloadSource,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        switch (payloadSource)
        {
            case ExplicitPayloadSource.In:
            {
                var payload = await InputResolver.TryReadPayloadAsync(inPath, inputFormat, cancellationToken);
                if (!payload.Ok)
                {
                    CliOutput.WriteValidationError(context, payload.Error);
                    return (false, null);
                }

                if (!TryNormalizeIssueInputPayload(payload.Payload, inputFormat, context, out var normalizedBody))
                {
                    return (false, null);
                }

                if (string.IsNullOrWhiteSpace(normalizedBody))
                {
                    CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
                    return (false, null);
                }

                if (!JsonPayloadPipeline.TryParseJsonObject(normalizedBody, "--in", out var inPayloadObject, out var parseError))
                {
                    CliOutput.WriteValidationError(context, parseError);
                    return (false, null);
                }

                return (true, inPayloadObject);
            }
            case ExplicitPayloadSource.Body:
            {
                if (!JsonPayloadPipeline.TryParseJsonObject(body!, "--body", out var bodyObject, out var parseBodyError))
                {
                    CliOutput.WriteValidationError(context, parseBodyError);
                    return (false, null);
                }

                return (true, bodyObject);
            }
            case ExplicitPayloadSource.BodyFile:
            {
                if (!JsonPayloadPipeline.TryReadJsonObjectFile(bodyFile!, "--body-file", out var bodyFileObject, out var bodyFileError))
                {
                    CliOutput.WriteValidationError(context, bodyFileError);
                    return (false, null);
                }

                return (true, bodyFileObject);
            }
            default:
                return (true, JsonPayloadPipeline.ParseDefaultPayload(defaultPayload));
        }
    }

    private static async Task<(bool Ok, JsonObject? Payload)> TryResolveIssueTransitionBasePayloadAsync(
        string? inPath,
        string? body,
        string? bodyFile,
        InputFormat inputFormat,
        ExplicitPayloadSource payloadSource,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        switch (payloadSource)
        {
            case ExplicitPayloadSource.In:
            {
                var payload = await InputResolver.TryReadPayloadAsync(inPath, inputFormat, cancellationToken);
                if (!payload.Ok)
                {
                    CliOutput.WriteValidationError(context, payload.Error);
                    return (false, null);
                }

                if (!TryNormalizeTransitionInputPayload(payload.Payload, inputFormat, context, out var normalizedBody))
                {
                    return (false, null);
                }

                if (string.IsNullOrWhiteSpace(normalizedBody))
                {
                    CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
                    return (false, null);
                }

                if (!JsonPayloadPipeline.TryParseJsonObject(normalizedBody, "--in", out var transitionPayload, out var parseError))
                {
                    CliOutput.WriteValidationError(context, parseError);
                    return (false, null);
                }

                return (true, transitionPayload);
            }
            case ExplicitPayloadSource.Body:
            {
                if (!JsonPayloadPipeline.TryParseJsonObject(body!, "--body", out var bodyObject, out var parseBodyError))
                {
                    CliOutput.WriteValidationError(context, parseBodyError);
                    return (false, null);
                }

                return (true, bodyObject);
            }
            case ExplicitPayloadSource.BodyFile:
            {
                if (!JsonPayloadPipeline.TryReadJsonObjectFile(bodyFile!, "--body-file", out var bodyFileObject, out var bodyFileError))
                {
                    CliOutput.WriteValidationError(context, bodyFileError);
                    return (false, null);
                }

                return (true, bodyFileObject);
            }
            default:
                return (true, JsonPayloadPipeline.ParseDefaultPayload("{\"transition\":{},\"fields\":{},\"update\":{}}"));
        }
    }

    private static async Task<(bool Ok, JsonObject? Payload)> TryResolveCommentBasePayloadAsync(
        string? inPath,
        string? body,
        string? bodyFile,
        InputFormat inputFormat,
        ExplicitPayloadSource payloadSource,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        switch (payloadSource)
        {
            case ExplicitPayloadSource.In:
            {
                var payload = await InputResolver.TryReadPayloadAsync(inPath, inputFormat, cancellationToken);
                if (!payload.Ok)
                {
                    CliOutput.WriteValidationError(context, payload.Error);
                    return (false, null);
                }

                if (!TryBuildCommentBasePayloadFromInput(payload.Payload, inputFormat, context, out var commentPayload))
                {
                    return (false, null);
                }

                return (true, commentPayload);
            }
            case ExplicitPayloadSource.Body:
            {
                if (!JsonPayloadPipeline.TryParseJsonObject(body!, "--body", out var bodyObject, out var parseBodyError))
                {
                    CliOutput.WriteValidationError(context, parseBodyError);
                    return (false, null);
                }

                return (true, bodyObject);
            }
            case ExplicitPayloadSource.BodyFile:
            {
                if (!JsonPayloadPipeline.TryReadJsonObjectFile(bodyFile!, "--body-file", out var bodyFileObject, out var bodyFileError))
                {
                    CliOutput.WriteValidationError(context, bodyFileError);
                    return (false, null);
                }

                return (true, bodyFileObject);
            }
            default:
                return (true, JsonPayloadPipeline.ParseDefaultPayload("{\"body\":{}}"));
        }
    }

    private static bool TryBuildCommentBasePayloadFromInput(
        string? payload,
        InputFormat inputFormat,
        InvocationContext context,
        out JsonObject commentPayload)
    {
        commentPayload = JsonPayloadPipeline.ParseDefaultPayload("{\"body\":{}}");
        if (string.IsNullOrWhiteSpace(payload))
        {
            CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
            return false;
        }

        if (inputFormat == InputFormat.Adf)
        {
            if (!JsonPayloadPipeline.TryParseJsonObject(payload, "--in", out var adfBody, out var parseAdfError))
            {
                CliOutput.WriteValidationError(context, parseAdfError);
                return false;
            }

            JsonPayloadPipeline.SetNode(commentPayload, adfBody, "body");
            return true;
        }

        if (inputFormat == InputFormat.Markdown || inputFormat == InputFormat.Text)
        {
            JsonPayloadPipeline.SetNode(commentPayload, BuildCommentAdfTextNode(payload), "body");
            return true;
        }

        if (!JsonPayloadPipeline.TryParseJsonObject(payload, "--in", out var jsonPayload, out var parseJsonError))
        {
            CliOutput.WriteValidationError(context, parseJsonError);
            return false;
        }

        commentPayload = jsonPayload!;
        return true;
    }

    private static JsonObject BuildCommentAdfTextNode(string text)
    {
        return new JsonObject
        {
            ["type"] = "doc",
            ["version"] = 1,
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "paragraph",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = text
                        }
                    }
                }
            }
        };
    }

    private static bool HasIssueUpdateOperations(JsonObject payload)
    {
        foreach (var property in payload)
        {
            if (property.Value is null)
            {
                continue;
            }

            if (property.Key.Equals("fields", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value is JsonObject fields && fields.Count > 0)
                {
                    return true;
                }

                continue;
            }

            if (property.Key.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value is JsonObject update && update.Count > 0)
                {
                    return true;
                }

                continue;
            }

            if (JsonPayloadPipeline.HasMeaningfulNode(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasValidCommentBody(JsonObject payload)
    {
        return JsonPayloadPipeline.HasMeaningfulNode(JsonPayloadPipeline.TryGetNode(payload, "body"));
    }

}
