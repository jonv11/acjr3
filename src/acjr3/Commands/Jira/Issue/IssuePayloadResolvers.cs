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
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(inPath))
        {
            var payload = await InputResolver.TryReadPayloadAsync(inPath, cancellationToken);
            if (!payload.Ok)
            {
                CliOutput.WriteValidationError(context, payload.Error);
                return (false, null);
            }

            if (string.IsNullOrWhiteSpace(payload.Payload))
            {
                CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
                return (false, null);
            }

            if (!JsonPayloadPipeline.TryParseJsonObject(payload.Payload, "--in", out var inPayloadObject, out var parseError))
            {
                CliOutput.WriteValidationError(context, parseError);
                return (false, null);
            }

            return (true, inPayloadObject);
        }

        return (true, JsonPayloadPipeline.ParseDefaultPayload(defaultPayload));
    }

    private static async Task<(bool Ok, JsonObject? Payload)> TryResolveIssueTransitionBasePayloadAsync(
        string? inPath,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(inPath))
        {
            var payload = await InputResolver.TryReadPayloadAsync(inPath, cancellationToken);
            if (!payload.Ok)
            {
                CliOutput.WriteValidationError(context, payload.Error);
                return (false, null);
            }

            if (string.IsNullOrWhiteSpace(payload.Payload))
            {
                CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
                return (false, null);
            }

            if (!JsonPayloadPipeline.TryParseJsonObject(payload.Payload, "--in", out var transitionPayload, out var parseError))
            {
                CliOutput.WriteValidationError(context, parseError);
                return (false, null);
            }

            return (true, transitionPayload);
        }

        return (true, JsonPayloadPipeline.ParseDefaultPayload("{\"transition\":{},\"fields\":{},\"update\":{}}"));
    }

    private static async Task<(bool Ok, JsonObject? Payload)> TryResolveCommentBasePayloadAsync(
        string? inPath,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(inPath))
        {
            var payload = await InputResolver.TryReadPayloadAsync(inPath, cancellationToken);
            if (!payload.Ok)
            {
                CliOutput.WriteValidationError(context, payload.Error);
                return (false, null);
            }

            if (!TryBuildCommentBasePayloadFromInput(payload.Payload, context, out var commentPayload))
            {
                return (false, null);
            }

            return (true, commentPayload);
        }

        return (true, JsonPayloadPipeline.ParseDefaultPayload("{\"body\":{}}"));
    }

    private static bool TryBuildCommentBasePayloadFromInput(
        string? payload,
        InvocationContext context,
        out JsonObject commentPayload)
    {
        commentPayload = JsonPayloadPipeline.ParseDefaultPayload("{\"body\":{}}");
        if (string.IsNullOrWhiteSpace(payload))
        {
            CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
            return false;
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
