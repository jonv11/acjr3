using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class IssueLinkCommands
{
    public static Command BuildIssueLinkCommand(IServiceProvider services)
    {
        var issueLink = new Command("issuelink", "Create a Jira issue link.");
        var typeOpt = new Option<string?>("--type", "Issue link type name (for example Blocks)");
        var inwardOpt = new Option<string?>("--inward", "Inward issue key (for example ACJ-123)");
        var outwardOpt = new Option<string?>("--outward", "Outward issue key (for example ACJ-456)");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success", "Allow 4xx/5xx responses without forcing a non-zero exit.");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        issueLink.AddOption(typeOpt);
        issueLink.AddOption(inwardOpt);
        issueLink.AddOption(outwardOpt);
        issueLink.AddOption(inOpt);
        issueLink.AddOption(yesOpt);
        issueLink.AddOption(forceOpt);
        issueLink.AddOption(allowNonSuccessOpt);
        issueLink.AddOption(verboseOpt);

        issueLink.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            JsonObject payloadObject = JsonPayloadPipeline.ParseDefaultPayload("{\"type\":{},\"inwardIssue\":{},\"outwardIssue\":{}}");
            var inPath = parseResult.GetValueForOption(inOpt);
            if (!string.IsNullOrWhiteSpace(inPath))
            {
                var payload = await InputResolver.TryReadPayloadAsync(inPath, context.GetCancellationToken());
                if (!payload.Ok)
                {
                    CliOutput.WriteValidationError(context, payload.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(payload.Payload))
                {
                    CliOutput.WriteValidationError(context, "--in was provided but no payload was read.");
                    return;
                }

                if (!JsonPayloadPipeline.TryParseJsonObject(payload.Payload, "--in", out var inPayloadObject, out var parseInError))
                {
                    CliOutput.WriteValidationError(context, parseInError);
                    return;
                }

                payloadObject = inPayloadObject!;
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
                !parseResult.GetValueForOption(allowNonSuccessOpt),
                false,
                false,
                parseResult.GetValueForOption(yesOpt) || parseResult.GetValueForOption(forceOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return issueLink;
    }
}





