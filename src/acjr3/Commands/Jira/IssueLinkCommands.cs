using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class IssueLinkCommands
{
    public static Command BuildIssueLinkCommand(IServiceProvider services)
    {
        var issueLink = new Command("issuelink", "Create Jira issue links (POST /rest/api/3/issueLink). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var typeOpt = new Option<string?>("--type", "Issue link type name (for example Blocks)");
        var inwardOpt = new Option<string?>("--inward", "Inward issue key (for example ACJ-123)");
        var outwardOpt = new Option<string?>("--outward", "Outward issue key (for example ACJ-456)");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        issueLink.AddOption(typeOpt);
        issueLink.AddOption(inwardOpt);
        issueLink.AddOption(outwardOpt);
        issueLink.AddOption(bodyOpt);
        issueLink.AddOption(bodyFileOpt);
        issueLink.AddOption(inOpt);
        issueLink.AddOption(inputFormatOpt);
        issueLink.AddOption(yesOpt);
        issueLink.AddOption(forceOpt);
        issueLink.AddOption(failOnNonSuccessOpt);
        issueLink.AddOption(verboseOpt);

        issueLink.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            if (!InputResolver.TryParseFormat(parseResult.GetValueForOption(inputFormatOpt), out var inputFormat, out var formatError))
            {
                CliOutput.WriteValidationError(context, formatError);
                return;
            }

            if (!InputResolver.TryResolveExplicitPayloadSource(
                    parseResult.GetValueForOption(inOpt),
                    parseResult.GetValueForOption(bodyOpt),
                    parseResult.GetValueForOption(bodyFileOpt),
                    out var payloadSource,
                    out var sourceError))
            {
                CliOutput.WriteValidationError(context, sourceError);
                return;
            }

            JsonObject? payloadObject = null;
            switch (payloadSource)
            {
                case ExplicitPayloadSource.In:
                {
                    var payload = await InputResolver.TryReadPayloadAsync(parseResult.GetValueForOption(inOpt), inputFormat, context.GetCancellationToken());
                    if (!payload.Ok)
                    {
                        CliOutput.WriteValidationError(context, payload.Error);
                        return;
                    }

                    if (!TryNormalizeIssueLinkInputPayload(payload.Payload, inputFormat, context, out var normalizedInBody))
                    {
                        return;
                    }

                    if (!JsonPayloadPipeline.TryParseJsonObject(normalizedInBody!, "--in", out payloadObject, out var parseInError))
                    {
                        CliOutput.WriteValidationError(context, parseInError);
                        return;
                    }

                    break;
                }
                case ExplicitPayloadSource.Body:
                {
                    var body = parseResult.GetValueForOption(bodyOpt)!;
                    if (!JsonPayloadPipeline.TryParseJsonObject(body, "--body", out payloadObject, out var parseBodyError))
                    {
                        CliOutput.WriteValidationError(context, parseBodyError);
                        return;
                    }

                    break;
                }
                case ExplicitPayloadSource.BodyFile:
                {
                    if (!JsonPayloadPipeline.TryReadJsonObjectFile(parseResult.GetValueForOption(bodyFileOpt)!, "--body-file", out payloadObject, out var fileError))
                    {
                        CliOutput.WriteValidationError(context, fileError);
                        return;
                    }

                    break;
                }
                default:
                    payloadObject = JsonPayloadPipeline.ParseDefaultPayload("{\"type\":{},\"inwardIssue\":{},\"outwardIssue\":{}}");
                    break;
            }

            if (payloadObject is null)
            {
                CliOutput.WriteValidationError(context, "Failed to initialize issue link payload.");
                return;
            }

            var type = parseResult.GetValueForOption(typeOpt);
            if (!string.IsNullOrWhiteSpace(type))
            {
                JsonPayloadPipeline.SetString(payloadObject, type, "type", "name");
            }

            var inward = parseResult.GetValueForOption(inwardOpt);
            if (!string.IsNullOrWhiteSpace(inward))
            {
                JsonPayloadPipeline.SetString(payloadObject, inward, "inwardIssue", "key");
            }

            var outward = parseResult.GetValueForOption(outwardOpt);
            if (!string.IsNullOrWhiteSpace(outward))
            {
                JsonPayloadPipeline.SetString(payloadObject, outward, "outwardIssue", "key");
            }

            if (string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "type", "name"))
                || string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "inwardIssue", "key"))
                || string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "outwardIssue", "key")))
            {
                CliOutput.WriteValidationError(context, "Final payload must include type.name, inwardIssue.key, and outwardIssue.key.");
                return;
            }

            var resolvedBody = JsonPayloadPipeline.Serialize(payloadObject);

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                "/rest/api/3/issueLink",
                new List<KeyValuePair<string, string>>(),
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                resolvedBody,
                null,
                outputPreferences,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                parseResult.GetValueForOption(yesOpt) || parseResult.GetValueForOption(forceOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return issueLink;
    }

    private static bool TryNormalizeIssueLinkInputPayload(
        string? payload,
        InputFormat format,
        InvocationContext context,
        out string? normalizedBody)
    {
        normalizedBody = payload;
        if (string.IsNullOrWhiteSpace(payload))
        {
            CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
            return false;
        }

        if (format == InputFormat.Markdown || format == InputFormat.Text)
        {
            CliOutput.WriteValidationError(context, "--input-format for issuelink --in must be json or adf.");
            return false;
        }

        return true;
    }
}





