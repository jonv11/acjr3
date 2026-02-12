using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class IssueCommands
{
    private enum DescriptionFileFormat
    {
        Text,
        Adf
    }

    private enum FieldFileFormat
    {
        Json,
        Adf
    }

    public static Command BuildIssueCommand(IServiceProvider services)
    {
        var issue = new Command("issue", "Jira issue commands");
        issue.AddCommand(BuildCreateCommand(services));
        issue.AddCommand(BuildUpdateCommand(services));
        issue.AddCommand(BuildDeleteCommand(services));
        issue.AddCommand(BuildViewCommand(services));
        issue.AddCommand(BuildCommentCommand(services));
        issue.AddCommand(BuildTransitionCommand(services));
        issue.AddCommand(BuildCreateMetaCommand(services));
        issue.AddCommand(BuildEditMetaCommand(services));
        return issue;
    }

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
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
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

    private static Command BuildUpdateCommand(IServiceProvider services)
    {
        var update = new Command("update", "Update a Jira issue (PUT /rest/api/3/issue/{issueIdOrKey}). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var projectOpt = new Option<string?>("--project", "Project key");
        var summaryOpt = new Option<string?>("--summary", "Issue summary");
        var typeOpt = new Option<string?>("--type", "Issue type (for example, Bug, Task)");
        var descriptionOpt = new Option<string?>("--description", "Issue description");
        var fieldOpt = new Option<string?>("--field", "Field key to update when using --field-file (for example, description, customfield_123)");
        var fieldFileOpt = new Option<string?>("--field-file", "Read field value from file path");
        var fieldFormatOpt = new Option<string>("--field-format", () => "json", "Field file format: json|adf");
        var assigneeOpt = new Option<string?>("--assignee", "Assignee accountId");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var notifyUsersOpt = new Option<string?>("--notify-users", "Jira query parameter notifyUsers=true|false");
        var overrideScreenSecurityOpt = new Option<string?>("--override-screen-security", "Jira query parameter overrideScreenSecurity=true|false");
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag", "Jira query parameter overrideEditableFlag=true|false");
        var returnIssueOpt = new Option<string?>("--return-issue", "Jira query parameter returnIssue=true|false");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        update.AddArgument(keyArg);
        update.AddOption(projectOpt);
        update.AddOption(summaryOpt);
        update.AddOption(typeOpt);
        update.AddOption(descriptionOpt);
        update.AddOption(fieldOpt);
        update.AddOption(fieldFileOpt);
        update.AddOption(fieldFormatOpt);
        update.AddOption(assigneeOpt);
        update.AddOption(bodyOpt);
        update.AddOption(bodyFileOpt);
        update.AddOption(inOpt);
        update.AddOption(inputFormatOpt);
        update.AddOption(notifyUsersOpt);
        update.AddOption(overrideScreenSecurityOpt);
        update.AddOption(overrideEditableFlagOpt);
        update.AddOption(returnIssueOpt);
        update.AddOption(yesOpt);
        update.AddOption(forceOpt);
        update.AddOption(failOnNonSuccessOpt);
        update.AddOption(verboseOpt);

        update.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
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

            var updateBasePayload = await TryResolveIssueJsonBasePayloadAsync(
                "{\"fields\":{}}",
                parseResult.GetValueForOption(inOpt),
                parseResult.GetValueForOption(bodyOpt),
                parseResult.GetValueForOption(bodyFileOpt),
                inputFormat,
                payloadSource,
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
            AddBooleanQuery(query, "notifyUsers", notifyUsers);
            AddBooleanQuery(query, "overrideScreenSecurity", overrideScreenSecurity);
            AddBooleanQuery(query, "overrideEditableFlag", overrideEditableFlag);
            AddBooleanQuery(query, "returnIssue", returnIssue);

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
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                parseResult.GetValueForOption(yesOpt) || parseResult.GetValueForOption(forceOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return update;
    }

    private static Command BuildDeleteCommand(IServiceProvider services)
    {
        var delete = new Command("delete", "Delete a Jira issue (DELETE /rest/api/3/issue/{issueIdOrKey}).");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var deleteSubtasksOpt = new Option<string?>("--delete-subtasks", "Jira query parameter deleteSubtasks=true|false");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        delete.AddArgument(keyArg);
        delete.AddOption(deleteSubtasksOpt);
        delete.AddOption(yesOpt);
        delete.AddOption(forceOpt);
        delete.AddOption(failOnNonSuccessOpt);
        delete.AddOption(verboseOpt);

        delete.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            if (!TryParseBooleanOption(parseResult.GetValueForOption(deleteSubtasksOpt), "--delete-subtasks", context, out var deleteSubtasks))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddBooleanQuery(query, "deleteSubtasks", deleteSubtasks);

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Delete,
                $"/rest/api/3/issue/{key}",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
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

        return delete;
    }

    private static Command BuildCreateMetaCommand(IServiceProvider services)
    {
        var createMeta = new Command("createmeta", "Get issue create metadata");
        var projectIdsOpt = new Option<string?>("--project-ids", "Comma-separated project IDs");
        var projectKeysOpt = new Option<string?>("--project-keys", "Comma-separated project keys");
        var issueTypeIdsOpt = new Option<string?>("--issuetype-ids", "Comma-separated issue type IDs");
        var issueTypeNamesOpt = new Option<string?>("--issuetype-names", "Comma-separated issue type names");
        var expandOpt = new Option<string?>("--expand", "Expand create metadata fields");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        createMeta.AddOption(projectIdsOpt);
        createMeta.AddOption(projectKeysOpt);
        createMeta.AddOption(issueTypeIdsOpt);
        createMeta.AddOption(issueTypeNamesOpt);
        createMeta.AddOption(expandOpt);
        createMeta.AddOption(failOnNonSuccessOpt);
        createMeta.AddOption(verboseOpt);

        createMeta.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddStringQuery(query, "projectIds", parseResult.GetValueForOption(projectIdsOpt));
            AddStringQuery(query, "projectKeys", parseResult.GetValueForOption(projectKeysOpt));
            AddStringQuery(query, "issuetypeIds", parseResult.GetValueForOption(issueTypeIdsOpt));
            AddStringQuery(query, "issuetypeNames", parseResult.GetValueForOption(issueTypeNamesOpt));
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/issue/createmeta",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
                null,
                outputPreferences,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return createMeta;
    }

    private static Command BuildEditMetaCommand(IServiceProvider services)
    {
        var editMeta = new Command("editmeta", "Get issue edit metadata");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var overrideScreenSecurityOpt = new Option<string?>("--override-screen-security", "Override screen security (true|false)");
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag", "Override editable flag (true|false)");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        editMeta.AddArgument(keyArg);
        editMeta.AddOption(overrideScreenSecurityOpt);
        editMeta.AddOption(overrideEditableFlagOpt);
        editMeta.AddOption(failOnNonSuccessOpt);
        editMeta.AddOption(verboseOpt);

        editMeta.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(overrideScreenSecurityOpt), "--override-screen-security", context, out var overrideScreenSecurity)
                || !TryParseBooleanOption(parseResult.GetValueForOption(overrideEditableFlagOpt), "--override-editable-flag", context, out var overrideEditableFlag))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddBooleanQuery(query, "overrideScreenSecurity", overrideScreenSecurity);
            AddBooleanQuery(query, "overrideEditableFlag", overrideEditableFlag);

            var key = parseResult.GetValueForArgument(keyArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}/editmeta",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
                null,
                outputPreferences,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return editMeta;
    }


    private static Command BuildViewCommand(IServiceProvider services)
    {
        var view = new Command("view", "Show details for a specific issue");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        view.AddArgument(keyArg);
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        var fieldsOpt = new Option<string?>("--fields", "Comma-separated list of fields to include in the reply (for example, summary,description)");
        var fieldsByKeysOpt = new Option<string?>("--fields-by-keys", "Interpret fields in --fields by key (true|false)");
        var expandOpt = new Option<string?>("--expand", "Expand issue response entities");
        var propertiesOpt = new Option<string?>("--properties", "Comma-separated issue properties to include");
        var updateHistoryOpt = new Option<string?>("--update-history", "Update issue view history (true|false)");
        var failFastOpt = new Option<string?>("--fail-fast", "Fail fast on invalid request details (true|false)");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        view.AddOption(verboseOpt);
        view.AddOption(fieldsOpt);
        view.AddOption(fieldsByKeysOpt);
        view.AddOption(expandOpt);
        view.AddOption(propertiesOpt);
        view.AddOption(updateHistoryOpt);
        view.AddOption(failFastOpt);
        view.AddOption(failOnNonSuccessOpt);
        view.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var fields = parseResult.GetValueForOption(fieldsOpt);
            if (!TryParseBooleanOption(parseResult.GetValueForOption(fieldsByKeysOpt), "--fields-by-keys", context, out var fieldsByKeys)
                || !TryParseBooleanOption(parseResult.GetValueForOption(updateHistoryOpt), "--update-history", context, out var updateHistory)
                || !TryParseBooleanOption(parseResult.GetValueForOption(failFastOpt), "--fail-fast", context, out var failFast))
            {
                return;
            }

            var queryParams = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrWhiteSpace(fields))
            {
                queryParams.Add(new KeyValuePair<string, string>("fields", fields));
            }

            AddBooleanQuery(queryParams, "fieldsByKeys", fieldsByKeys);
            AddStringQuery(queryParams, "expand", parseResult.GetValueForOption(expandOpt));
            AddStringQuery(queryParams, "properties", parseResult.GetValueForOption(propertiesOpt));
            AddBooleanQuery(queryParams, "updateHistory", updateHistory);
            AddBooleanQuery(queryParams, "failFast", failFast);

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}",
                queryParams,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
                null,
                outputPreferences,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
        return view;
    }

    private static Command BuildCommentCommand(IServiceProvider services)
    {
        var comment = new Command("comment", "Issue comment commands");
        comment.AddCommand(BuildCommentAddCommand(services));
        comment.AddCommand(BuildCommentListCommand(services));
        comment.AddCommand(BuildCommentGetCommand(services));
        comment.AddCommand(BuildCommentUpdateCommand(services));
        comment.AddCommand(BuildCommentDeleteCommand(services));

        return comment;
    }

    private static Command BuildCommentAddCommand(IServiceProvider services)
    {
        var add = new Command("add", "Add a comment to an issue (POST /rest/api/3/issue/{issueIdOrKey}/comment). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text", "Comment text");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        add.AddArgument(keyArg);
        add.AddOption(textOpt);
        add.AddOption(bodyOpt);
        add.AddOption(bodyFileOpt);
        add.AddOption(inOpt);
        add.AddOption(inputFormatOpt);
        add.AddOption(expandOpt);
        add.AddOption(yesOpt);
        add.AddOption(forceOpt);
        add.AddOption(failOnNonSuccessOpt);
        add.AddOption(verboseOpt);

        add.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
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

            var addCommentBasePayload = await TryResolveCommentBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                parseResult.GetValueForOption(bodyOpt),
                parseResult.GetValueForOption(bodyFileOpt),
                inputFormat,
                payloadSource,
                context,
                context.GetCancellationToken());
            if (!addCommentBasePayload.Ok)
            {
                return;
            }
            var payloadObject = addCommentBasePayload.Payload!;

            var text = parseResult.GetValueForOption(textOpt);
            if (!string.IsNullOrWhiteSpace(text))
            {
                JsonPayloadPipeline.SetNode(payloadObject, BuildCommentAdfTextNode(text), "body");
            }

            if (!HasValidCommentBody(payloadObject))
            {
                CliOutput.WriteValidationError(context, "Final payload must include a non-empty body.");
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                $"/rest/api/3/issue/{key}/comment",
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

        return add;
    }

    private static Command BuildCommentListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List comments for an issue");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum comments to return");
        var orderByOpt = new Option<string?>("--order-by", "Order by expression");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        list.AddArgument(keyArg);
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(orderByOpt);
        list.AddOption(expandOpt);
        list.AddOption(failOnNonSuccessOpt);
        list.AddOption(verboseOpt);

        list.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var startAt = parseResult.GetValueForOption(startAtOpt);
            if (startAt.HasValue && startAt.Value < 0)
            {
                CliOutput.WriteValidationError(context, "--start-at must be zero or greater.");
                return;
            }

            var maxResults = parseResult.GetValueForOption(maxResultsOpt);
            if (maxResults.HasValue && maxResults.Value <= 0)
            {
                CliOutput.WriteValidationError(context, "--max-results must be greater than zero.");
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddIntQuery(query, "startAt", startAt);
            AddIntQuery(query, "maxResults", maxResults);
            AddStringQuery(query, "orderBy", parseResult.GetValueForOption(orderByOpt));
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}/comment",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
                null,
                outputPreferences,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return list;
    }

    private static Command BuildCommentGetCommand(IServiceProvider services)
    {
        var get = new Command("get", "Get one comment from an issue");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        get.AddArgument(keyArg);
        get.AddArgument(idArg);
        get.AddOption(expandOpt);
        get.AddOption(failOnNonSuccessOpt);
        get.AddOption(verboseOpt);

        get.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var id = parseResult.GetValueForArgument(idArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}/comment/{id}",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
                null,
                outputPreferences,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return get;
    }

    private static Command BuildCommentUpdateCommand(IServiceProvider services)
    {
        var update = new Command("update", "Update an issue comment (PUT /rest/api/3/issue/{issueIdOrKey}/comment/{id}). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text", "Comment text");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var notifyUsersOpt = new Option<string?>("--notify-users", "Notify users (true|false)");
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag", "Override editable flag (true|false)");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        update.AddArgument(keyArg);
        update.AddArgument(idArg);
        update.AddOption(textOpt);
        update.AddOption(bodyOpt);
        update.AddOption(bodyFileOpt);
        update.AddOption(inOpt);
        update.AddOption(inputFormatOpt);
        update.AddOption(notifyUsersOpt);
        update.AddOption(overrideEditableFlagOpt);
        update.AddOption(expandOpt);
        update.AddOption(yesOpt);
        update.AddOption(forceOpt);
        update.AddOption(failOnNonSuccessOpt);
        update.AddOption(verboseOpt);

        update.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
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

            var updateCommentBasePayload = await TryResolveCommentBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                parseResult.GetValueForOption(bodyOpt),
                parseResult.GetValueForOption(bodyFileOpt),
                inputFormat,
                payloadSource,
                context,
                context.GetCancellationToken());
            if (!updateCommentBasePayload.Ok)
            {
                return;
            }
            var payloadObject = updateCommentBasePayload.Payload!;

            var text = parseResult.GetValueForOption(textOpt);
            if (!string.IsNullOrWhiteSpace(text))
            {
                JsonPayloadPipeline.SetNode(payloadObject, BuildCommentAdfTextNode(text), "body");
            }

            if (!HasValidCommentBody(payloadObject))
            {
                CliOutput.WriteValidationError(context, "Final payload must include a non-empty body.");
                return;
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(notifyUsersOpt), "--notify-users", context, out var notifyUsers)
                || !TryParseBooleanOption(parseResult.GetValueForOption(overrideEditableFlagOpt), "--override-editable-flag", context, out var overrideEditableFlag))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddBooleanQuery(query, "notifyUsers", notifyUsers);
            AddBooleanQuery(query, "overrideEditableFlag", overrideEditableFlag);
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var id = parseResult.GetValueForArgument(idArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Put,
                $"/rest/api/3/issue/{key}/comment/{id}",
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

        return update;
    }

    private static Command BuildCommentDeleteCommand(IServiceProvider services)
    {
        var delete = new Command("delete", "Delete an issue comment (DELETE /rest/api/3/issue/{issueIdOrKey}/comment/{id}).");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        delete.AddArgument(keyArg);
        delete.AddArgument(idArg);
        delete.AddOption(yesOpt);
        delete.AddOption(forceOpt);
        delete.AddOption(failOnNonSuccessOpt);
        delete.AddOption(verboseOpt);

        delete.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var id = parseResult.GetValueForArgument(idArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Delete,
                $"/rest/api/3/issue/{key}/comment/{id}",
                new List<KeyValuePair<string, string>>(),
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
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

        return delete;
    }

    private static Command BuildTransitionCommand(IServiceProvider services)
    {
        var transition = new Command("transition", "Issue transition commands (POST /rest/api/3/issue/{issueIdOrKey}/transitions). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var toOpt = new Option<string?>("--to", "Target transition name (for example, Done)");
        var idOpt = new Option<string?>("--id", "Target transition ID");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        transition.AddArgument(keyArg);
        transition.AddOption(toOpt);
        transition.AddOption(idOpt);
        transition.AddOption(bodyOpt);
        transition.AddOption(bodyFileOpt);
        transition.AddOption(inOpt);
        transition.AddOption(inputFormatOpt);
        transition.AddOption(yesOpt);
        transition.AddOption(forceOpt);
        transition.AddOption(failOnNonSuccessOpt);
        transition.AddOption(verboseOpt);
        transition.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var to = parseResult.GetValueForOption(toOpt);
            var id = parseResult.GetValueForOption(idOpt);
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

            var transitionBasePayload = await TryResolveIssueTransitionBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                parseResult.GetValueForOption(bodyOpt),
                parseResult.GetValueForOption(bodyFileOpt),
                inputFormat,
                payloadSource,
                context,
                context.GetCancellationToken());
            if (!transitionBasePayload.Ok)
            {
                return;
            }
            var payloadObject = transitionBasePayload.Payload!;

            if (!string.IsNullOrWhiteSpace(to) && !string.IsNullOrWhiteSpace(id))
            {
                CliOutput.WriteValidationError(context, "Provide either --to or --id, not both.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                JsonPayloadPipeline.SetString(payloadObject, id, "transition", "id");
            }
            else if (!string.IsNullOrWhiteSpace(to))
            {
                var transitionId = await ResolveTransitionIdByNameAsync(
                    services,
                    config!,
                    key,
                    to,
                    logger,
                    context.GetCancellationToken());
                if (string.IsNullOrWhiteSpace(transitionId))
                {
                    CliOutput.WriteValidationError(context, $"Could not resolve transition '{to}' for issue '{key}'.");
                    return;
                }

                JsonPayloadPipeline.SetString(payloadObject, transitionId, "transition", "id");
            }

            if (string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "transition", "id")))
            {
                CliOutput.WriteValidationError(context, "Final payload must include transition.id (set --id/--to or provide it in the base payload).");
                return;
            }

            var transitionOptions = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                $"/rest/api/3/issue/{key}/transitions",
                new List<KeyValuePair<string, string>>(),
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
            var exitCode = await executor.ExecuteAsync(config!, transitionOptions, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        transition.AddCommand(BuildTransitionListCommand(services));
        return transition;
    }

    private static Command BuildTransitionListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List available transitions for an issue");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var expandOpt = new Option<string?>("--expand", "Expand transition response entities");
        var transitionIdOpt = new Option<string?>("--transition-id", "Filter by transition ID");
        var skipRemoteOnlyConditionOpt = new Option<string?>("--skip-remote-only-condition", "Skip remote-only condition check (true|false)");
        var includeUnavailableTransitionsOpt = new Option<string?>("--include-unavailable-transitions", "Include unavailable transitions (true|false)");
        var sortByOpsBarAndStatusOpt = new Option<string?>("--sort-by-ops-bar-and-status", "Sort by ops bar and status (true|false)");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        list.AddArgument(keyArg);
        list.AddOption(expandOpt);
        list.AddOption(transitionIdOpt);
        list.AddOption(skipRemoteOnlyConditionOpt);
        list.AddOption(includeUnavailableTransitionsOpt);
        list.AddOption(sortByOpsBarAndStatusOpt);
        list.AddOption(failOnNonSuccessOpt);
        list.AddOption(verboseOpt);

        list.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                CliOutput.WriteValidationError(context, configError);
                return;
            }

            if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
            {
                return;
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(skipRemoteOnlyConditionOpt), "--skip-remote-only-condition", context, out var skipRemoteOnlyCondition)
                || !TryParseBooleanOption(parseResult.GetValueForOption(includeUnavailableTransitionsOpt), "--include-unavailable-transitions", context, out var includeUnavailableTransitions)
                || !TryParseBooleanOption(parseResult.GetValueForOption(sortByOpsBarAndStatusOpt), "--sort-by-ops-bar-and-status", context, out var sortByOpsBarAndStatus))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));
            AddStringQuery(query, "transitionId", parseResult.GetValueForOption(transitionIdOpt));
            AddBooleanQuery(query, "skipRemoteOnlyCondition", skipRemoteOnlyCondition);
            AddBooleanQuery(query, "includeUnavailableTransitions", includeUnavailableTransitions);
            AddBooleanQuery(query, "sortByOpsBarAndStatus", sortByOpsBarAndStatus);

            var key = parseResult.GetValueForArgument(keyArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}/transitions",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
                null,
                outputPreferences,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return list;
    }

    private static async Task<string?> ResolveTransitionIdByNameAsync(
        IServiceProvider services,
        Acjr3Config config,
        string issueKey,
        string transitionName,
        IAppLogger logger,
        CancellationToken cancellationToken)
    {
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("acjr3");
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

        var url = UrlBuilder.Build(config.SiteUrl, $"/rest/api/3/issue/{issueKey}/transitions", []);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        var auth = services.GetRequiredService<AuthHeaderProvider>().Create(config);
        request.Headers.Authorization = auth;

        logger.Verbose($"Resolving transition id by name '{transitionName}' for issue {issueKey}");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Failed to resolve transition name '{transitionName}' on issue {issueKey}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            var text = Encoding.UTF8.GetString(payload);
            if (!string.IsNullOrWhiteSpace(text))
            {
                Console.Error.WriteLine(text);
            }

            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("transitions", out var transitions)
                || transitions.ValueKind != JsonValueKind.Array)
            {
                Console.Error.WriteLine("Transition response does not include a transitions array.");
                return null;
            }

            string? fallbackId = null;
            var availableNames = new List<string>();
            foreach (var item in transitions.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (!item.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var name = nameElement.GetString()!;
                availableNames.Add(name);
                if (name.Equals(transitionName, StringComparison.OrdinalIgnoreCase))
                {
                    fallbackId = idElement.GetString();
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackId))
            {
                return fallbackId;
            }

            Console.Error.WriteLine($"Transition name '{transitionName}' not found for issue {issueKey}.");
            if (availableNames.Count > 0)
            {
                Console.Error.WriteLine($"Available transitions: {string.Join(", ", availableNames)}");
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse transitions response: {ex.Message}");
            return null;
        }
    }

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

    private static void AddStringQuery(List<KeyValuePair<string, string>> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    private static void AddIntQuery(List<KeyValuePair<string, string>> query, string key, int? value)
    {
        if (value.HasValue)
        {
            query.Add(new KeyValuePair<string, string>(key, value.Value.ToString()));
        }
    }

    private static void AddBooleanQuery(List<KeyValuePair<string, string>> query, string key, bool? value)
    {
        if (value.HasValue)
        {
            query.Add(new KeyValuePair<string, string>(key, value.Value ? "true" : "false"));
        }
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






