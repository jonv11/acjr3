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
    private static Command BuildCreateCommand(IServiceProvider services)
    {
        var create = new Command("create", "Create a Jira issue (POST /rest/api/3/issue). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var projectArg = new Argument<string?>("project", () => null, "Project key (for example, TEST)") { Arity = ArgumentArity.ZeroOrOne };
        var projectOpt = new Option<string?>("--project", "Project key (for example, TEST)");
        var summaryOpt = new Option<string?>("--summary", "Issue summary");
        var typeOpt = new Option<string>("--type", () => "Task", "Issue type (for example, Bug, Task)");
        var descriptionOpt = new Option<string?>("--description", "Issue description");
        var descriptionFileOpt = new Option<string?>("--description-file", "Read description content from file path");
        var descriptionFormatOpt = new Option<string>("--description-format", () => "text", "Description file format: text|adf");
        var assigneeOpt = new Option<string?>("--assignee", "Assignee accountId");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var updateHistoryOpt = new Option<string?>("--update-history", "Jira query parameter updateHistory=true|false");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        create.AddArgument(projectArg);
        create.AddOption(projectOpt);
        create.AddOption(summaryOpt);
        create.AddOption(typeOpt);
        create.AddOption(descriptionOpt);
        create.AddOption(descriptionFileOpt);
        create.AddOption(descriptionFormatOpt);
        create.AddOption(assigneeOpt);
        create.AddOption(bodyOpt);
        create.AddOption(bodyFileOpt);
        create.AddOption(inOpt);
        create.AddOption(inputFormatOpt);
        create.AddOption(updateHistoryOpt);
        create.AddOption(yesOpt);
        create.AddOption(forceOpt);
        create.AddOption(failOnNonSuccessOpt);
        create.AddOption(verboseOpt);
        create.SetHandler(async (InvocationContext context) =>
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

            var createBasePayload = await TryResolveIssueJsonBasePayloadAsync(
                "{\"fields\":{\"issuetype\":{\"name\":\"Task\"}}}",
                parseResult.GetValueForOption(inOpt),
                parseResult.GetValueForOption(bodyOpt),
                parseResult.GetValueForOption(bodyFileOpt),
                inputFormat,
                payloadSource,
                context,
                context.GetCancellationToken());
            if (!createBasePayload.Ok)
            {
                return;
            }
            var payloadObject = createBasePayload.Payload!;

            if (!TryResolveProjectKey(
                    parseResult.GetValueForOption(projectOpt),
                    parseResult.GetValueForArgument(projectArg),
                    context,
                    out var project))
            {
                return;
            }

            var summary = parseResult.GetValueForOption(summaryOpt);
            var description = parseResult.GetValueForOption(descriptionOpt);
            if (!TryResolveDescriptionValue(
                    description,
                    parseResult.GetValueForOption(descriptionFileOpt),
                    parseResult.GetValueForOption(descriptionFormatOpt),
                    WasOptionSupplied(parseResult, "--description-format"),
                    context,
                    out var descriptionValue))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(project))
            {
                JsonPayloadPipeline.SetString(payloadObject, project, "fields", "project", "key");
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                JsonPayloadPipeline.SetString(payloadObject, summary, "fields", "summary");
            }

            if (WasOptionSupplied(parseResult, "--type"))
            {
                var type = parseResult.GetValueForOption(typeOpt);
                if (!string.IsNullOrWhiteSpace(type))
                {
                    JsonPayloadPipeline.SetString(payloadObject, type, "fields", "issuetype", "name");
                }
            }

            if (descriptionValue is string descriptionText)
            {
                if (!string.IsNullOrWhiteSpace(descriptionText))
                {
                    JsonPayloadPipeline.SetString(payloadObject, descriptionText, "fields", "description");
                }
            }
            else if (descriptionValue is not null)
            {
                JsonPayloadPipeline.SetNode(payloadObject, JsonSerializer.SerializeToNode(descriptionValue), "fields", "description");
            }

            var assignee = parseResult.GetValueForOption(assigneeOpt);
            if (!string.IsNullOrWhiteSpace(assignee))
            {
                JsonPayloadPipeline.SetString(payloadObject, assignee, "fields", "assignee", "accountId");
            }

            if (string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "fields", "project", "key"))
                || string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "fields", "summary"))
                || string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "fields", "issuetype", "name")))
            {
                CliOutput.WriteValidationError(context, "Final payload must include fields.project.key, fields.summary, and fields.issuetype.name.");
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            if (!TryParseBooleanOption(
                    parseResult.GetValueForOption(updateHistoryOpt),
                    "--update-history",
                    context,
                    out var updateHistory))
            {
                return;
            }

            if (updateHistory.HasValue)
            {
                query.Add(new KeyValuePair<string, string>("updateHistory", updateHistory.Value ? "true" : "false"));
            }

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                "/rest/api/3/issue",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                JsonPayloadPipeline.Serialize(payloadObject),
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
        return create;
    }
}

