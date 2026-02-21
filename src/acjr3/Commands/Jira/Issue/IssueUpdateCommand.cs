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
    private static Command BuildUpdateCommand(IServiceProvider services)
    {
        var update = new Command("update", "Update a Jira issue (PUT /rest/api/3/issue/{issueIdOrKey}). Starts from a default payload, optional --in base payload, then applies sugar flags.");
        var keyArg = new Argument<string>("key") { Description = "Issue key (for example, TEST-123)", Arity = ArgumentArity.ExactlyOne };
        var projectOpt = new Option<string?>("--project") { Description = "Project key" };
        var summaryOpt = new Option<string?>("--summary") { Description = "Issue summary" };
        var typeOpt = new Option<string?>("--type") { Description = "Issue type (for example, Bug, Task)" };
        var descriptionOpt = new Option<string?>("--description") { Description = "Issue description" };
        var fieldOpt = new Option<string?>("--field") { Description = "Field key to update when using --field-file (for example, description, customfield_123)" };
        var fieldFileOpt = new Option<string?>("--field-file") { Description = "Read field value from file path" };
        var fieldFormatOpt = new Option<string>("--field-format") { DefaultValueFactory = _ => "adf", Description = "Field file format: json|adf" };
        var assigneeOpt = new Option<string?>("--assignee") { Description = "Assignee accountId" };
        var inOpt = new Option<string?>("--in") { Description = "Path to request payload file, or '-' for stdin." };
        var notifyUsersOpt = new Option<string?>("--notify-users") { Description = "Jira query parameter notifyUsers=true|false" };
        var overrideScreenSecurityOpt = new Option<string?>("--override-screen-security") { Description = "Jira query parameter overrideScreenSecurity=true|false" };
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag") { Description = "Jira query parameter overrideEditableFlag=true|false" };
        var returnIssueOpt = new Option<string?>("--return-issue") { Description = "Jira query parameter returnIssue=true|false" };
        var yesOpt = new Option<bool>("--yes") { Description = "Confirm mutating operations." };
        var forceOpt = new Option<bool>("--force") { Description = "Force mutating operations." };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit." };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };

        update.AddArgument(keyArg);
        update.AddOption(projectOpt);
        update.AddOption(summaryOpt);
        update.AddOption(typeOpt);
        update.AddOption(descriptionOpt);
        update.AddOption(fieldOpt);
        update.AddOption(fieldFileOpt);
        update.AddOption(fieldFormatOpt);
        update.AddOption(assigneeOpt);
        update.AddOption(inOpt);
        update.AddOption(notifyUsersOpt);
        update.AddOption(overrideScreenSecurityOpt);
        update.AddOption(overrideEditableFlagOpt);
        update.AddOption(returnIssueOpt);
        update.AddOption(yesOpt);
        update.AddOption(forceOpt);
        update.AddOption(allowNonSuccessOpt);
        update.AddOption(verboseOpt);

        update.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);

            var updateBasePayload = await TryResolveIssueJsonBasePayloadAsync(
                "{\"fields\":{}}",
                parseResult.GetValueForOption(inOpt),
                context,
                context.GetCancellationToken());
            if (!updateBasePayload.Ok)
            {
                return;
            }
            var payloadObject = updateBasePayload.Payload!;

            if (!TryResolveNamedFieldFileValue(
                    parseResult.GetValueForOption(fieldOpt),
                    parseResult.GetValueForOption(fieldFileOpt),
                    parseResult.GetValueForOption(fieldFormatOpt),
                    WasOptionSupplied(parseResult, "--field-format"),
                    "--field",
                    context,
                    out var fieldName,
                    out var fieldValue))
            {
                return;
            }

            var project = parseResult.GetValueForOption(projectOpt);
            if (!string.IsNullOrWhiteSpace(project))
            {
                JsonPayloadPipeline.SetString(payloadObject, project, "fields", "project", "key");
            }

            var summary = parseResult.GetValueForOption(summaryOpt);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                JsonPayloadPipeline.SetString(payloadObject, summary, "fields", "summary");
            }

            var type = parseResult.GetValueForOption(typeOpt);
            if (!string.IsNullOrWhiteSpace(type))
            {
                JsonPayloadPipeline.SetString(payloadObject, type, "fields", "issuetype", "name");
            }

            var description = parseResult.GetValueForOption(descriptionOpt);
            if (!string.IsNullOrWhiteSpace(description))
            {
                JsonPayloadPipeline.SetString(payloadObject, description, "fields", "description");
            }

            var assignee = parseResult.GetValueForOption(assigneeOpt);
            if (!string.IsNullOrWhiteSpace(assignee))
            {
                JsonPayloadPipeline.SetString(payloadObject, assignee, "fields", "assignee", "accountId");
            }

            if (!string.IsNullOrWhiteSpace(fieldName) && fieldValue is not null)
            {
                JsonPayloadPipeline.SetNode(payloadObject, JsonSerializer.SerializeToNode(fieldValue), "fields", fieldName);
            }

            if (!HasIssueUpdateOperations(payloadObject))
            {
                CliOutput.WriteValidationError(context, "Final payload must include at least one issue update operation.");
                return;
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(notifyUsersOpt), "--notify-users", context, out var notifyUsers)
                || !TryParseBooleanOption(parseResult.GetValueForOption(overrideScreenSecurityOpt), "--override-screen-security", context, out var overrideScreenSecurity)
                || !TryParseBooleanOption(parseResult.GetValueForOption(overrideEditableFlagOpt), "--override-editable-flag", context, out var overrideEditableFlag)
                || !TryParseBooleanOption(parseResult.GetValueForOption(returnIssueOpt), "--return-issue", context, out var returnIssue))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddBoolean(query, "notifyUsers", notifyUsers);
            JiraQueryBuilder.AddBoolean(query, "overrideScreenSecurity", overrideScreenSecurity);
            JiraQueryBuilder.AddBoolean(query, "overrideEditableFlag", overrideEditableFlag);
            JiraQueryBuilder.AddBoolean(query, "returnIssue", returnIssue);

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Put,
                $"/rest/api/3/issue/{key}",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                JsonPayloadPipeline.Serialize(payloadObject),
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

        return update;
    }
}



