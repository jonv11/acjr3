using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class IssueCommands
{
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
        var create = new Command("create", "Create a Jira issue");
        var projectArg = new Argument<string?>("project", () => null, "Project key (e.g. TEST)") { Arity = ArgumentArity.ZeroOrOne };
        var projectOpt = new Option<string?>("--project", "Project key (e.g. TEST)");
        var summaryOpt = new Option<string?>("--summary", "Issue summary");
        var typeOpt = new Option<string>("--type", () => "Task", "Issue type (e.g. Bug, Task)");
        var descriptionOpt = new Option<string?>("--description", "Issue description");
        var descriptionAdfFileOpt = new Option<string?>("--description-adf-file", "Read description ADF JSON from file path");
        var assigneeOpt = new Option<string?>("--assignee", "Assignee accountId");
        var bodyOpt = new Option<string?>("--body", "Inline JSON payload matching Jira create-issue schema");
        var bodyFileOpt = new Option<string?>("--body-file", "Read JSON payload from file path");
        var updateHistoryOpt = new Option<string?>("--update-history", "Jira query parameter updateHistory=true|false");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        create.AddArgument(projectArg);
        create.AddOption(projectOpt);
        create.AddOption(summaryOpt);
        create.AddOption(typeOpt);
        create.AddOption(descriptionOpt);
        create.AddOption(descriptionAdfFileOpt);
        create.AddOption(assigneeOpt);
        create.AddOption(bodyOpt);
        create.AddOption(bodyFileOpt);
        create.AddOption(updateHistoryOpt);
        create.AddOption(rawOpt);
        create.AddOption(failOnNonSuccessOpt);
        create.AddOption(verboseOpt);
        create.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            var body = parseResult.GetValueForOption(bodyOpt);
            var bodyFile = parseResult.GetValueForOption(bodyFileOpt);
            if (!TryResolveBody(body, bodyFile, context, out var resolvedBody))
            {
                return;
            }

            if (!TryResolveProjectKey(
                    parseResult.GetValueForOption(projectOpt),
                    parseResult.GetValueForArgument(projectArg),
                    context,
                    out var project))
            {
                return;
            }

            var summary = parseResult.GetValueForOption(summaryOpt);
            var type = parseResult.GetValueForOption(typeOpt);
            var description = parseResult.GetValueForOption(descriptionOpt);
            if (!TryResolveOptionalJsonObjectFile(
                    parseResult.GetValueForOption(descriptionAdfFileOpt),
                    "--description-adf-file",
                    context,
                    out var descriptionAdf))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(description) && descriptionAdf.HasValue)
            {
                Console.Error.WriteLine("Use either --description or --description-adf-file, not both.");
                context.ExitCode = 1;
                return;
            }

            var assignee = parseResult.GetValueForOption(assigneeOpt);
            if (resolvedBody is null)
            {
                if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(summary))
                {
                    Console.Error.WriteLine("Either provide --body/--body-file, or provide project (<project> or --project) and --summary.");
                    context.ExitCode = 1;
                    return;
                }

                object? descriptionValue = descriptionAdf.HasValue ? descriptionAdf.Value : description;
                var fieldsDict = BuildFieldsDictionary(project, summary, type, descriptionValue, assignee);
                resolvedBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["fields"] = fieldsDict });
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
                resolvedBody,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
        return create;
    }

    private static Command BuildUpdateCommand(IServiceProvider services)
    {
        var update = new Command("update", "Update a Jira issue");
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var projectOpt = new Option<string?>("--project", "Project key");
        var summaryOpt = new Option<string?>("--summary", "Issue summary");
        var typeOpt = new Option<string?>("--type", "Issue type (e.g. Bug, Task)");
        var descriptionOpt = new Option<string?>("--description", "Issue description");
        var fieldOpt = new Option<string?>("--field", "Field key to update when using --field-adf-file (e.g. description, customfield_123)");
        var fieldAdfFileOpt = new Option<string?>("--field-adf-file", "Read field ADF JSON from file path");
        var assigneeOpt = new Option<string?>("--assignee", "Assignee accountId");
        var bodyOpt = new Option<string?>("--body", "Inline JSON payload matching Jira edit-issue schema");
        var bodyFileOpt = new Option<string?>("--body-file", "Read JSON payload from file path");
        var notifyUsersOpt = new Option<string?>("--notify-users", "Jira query parameter notifyUsers=true|false");
        var overrideScreenSecurityOpt = new Option<string?>("--override-screen-security", "Jira query parameter overrideScreenSecurity=true|false");
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag", "Jira query parameter overrideEditableFlag=true|false");
        var returnIssueOpt = new Option<string?>("--return-issue", "Jira query parameter returnIssue=true|false");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        update.AddArgument(keyArg);
        update.AddOption(projectOpt);
        update.AddOption(summaryOpt);
        update.AddOption(typeOpt);
        update.AddOption(descriptionOpt);
        update.AddOption(fieldOpt);
        update.AddOption(fieldAdfFileOpt);
        update.AddOption(assigneeOpt);
        update.AddOption(bodyOpt);
        update.AddOption(bodyFileOpt);
        update.AddOption(notifyUsersOpt);
        update.AddOption(overrideScreenSecurityOpt);
        update.AddOption(overrideEditableFlagOpt);
        update.AddOption(returnIssueOpt);
        update.AddOption(rawOpt);
        update.AddOption(failOnNonSuccessOpt);
        update.AddOption(verboseOpt);

        update.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var body = parseResult.GetValueForOption(bodyOpt);
            var bodyFile = parseResult.GetValueForOption(bodyFileOpt);
            if (!TryResolveBody(body, bodyFile, context, out var resolvedBody))
            {
                return;
            }

            if (!TryResolveNamedJsonObjectFile(
                    parseResult.GetValueForOption(fieldOpt),
                    parseResult.GetValueForOption(fieldAdfFileOpt),
                    "--field",
                    "--field-adf-file",
                    context,
                    out var adfFieldName,
                    out var adfFieldValue))
            {
                return;
            }

            if (resolvedBody is null)
            {
                var fields = BuildFieldsDictionary(
                    parseResult.GetValueForOption(projectOpt),
                    parseResult.GetValueForOption(summaryOpt),
                    parseResult.GetValueForOption(typeOpt),
                    parseResult.GetValueForOption(descriptionOpt),
                    parseResult.GetValueForOption(assigneeOpt));

                if (!string.IsNullOrWhiteSpace(adfFieldName) && adfFieldValue.HasValue)
                {
                    fields[adfFieldName] = adfFieldValue.Value;
                }

                if (fields.Count == 0)
                {
                    Console.Error.WriteLine("Provide at least one field option, --field/--field-adf-file, or supply --body/--body-file.");
                    context.ExitCode = 1;
                    return;
                }

                resolvedBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["fields"] = fields });
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
                resolvedBody,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return update;
    }

    private static Command BuildDeleteCommand(IServiceProvider services)
    {
        var delete = new Command("delete", "Delete a Jira issue");
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var deleteSubtasksOpt = new Option<string?>("--delete-subtasks", "Jira query parameter deleteSubtasks=true|false");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        delete.AddArgument(keyArg);
        delete.AddOption(deleteSubtasksOpt);
        delete.AddOption(failOnNonSuccessOpt);
        delete.AddOption(verboseOpt);

        delete.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
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
                false,
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

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
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        createMeta.AddOption(projectIdsOpt);
        createMeta.AddOption(projectKeysOpt);
        createMeta.AddOption(issueTypeIdsOpt);
        createMeta.AddOption(issueTypeNamesOpt);
        createMeta.AddOption(expandOpt);
        createMeta.AddOption(rawOpt);
        createMeta.AddOption(failOnNonSuccessOpt);
        createMeta.AddOption(verboseOpt);

        createMeta.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
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
                parseResult.GetValueForOption(rawOpt),
                false,
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
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var overrideScreenSecurityOpt = new Option<string?>("--override-screen-security", "Override screen security (true|false)");
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag", "Override editable flag (true|false)");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        editMeta.AddArgument(keyArg);
        editMeta.AddOption(overrideScreenSecurityOpt);
        editMeta.AddOption(overrideEditableFlagOpt);
        editMeta.AddOption(rawOpt);
        editMeta.AddOption(failOnNonSuccessOpt);
        editMeta.AddOption(verboseOpt);

        editMeta.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
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
                parseResult.GetValueForOption(rawOpt),
                false,
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
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        view.AddArgument(keyArg);
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        var fieldsOpt = new Option<string?>("--fields", "Comma-separated list of fields to include in the reply (e.g. summary,description)");
        var fieldsByKeysOpt = new Option<string?>("--fields-by-keys", "Interpret fields in --fields by key (true|false)");
        var expandOpt = new Option<string?>("--expand", "Expand issue response entities");
        var propertiesOpt = new Option<string?>("--properties", "Comma-separated issue properties to include");
        var updateHistoryOpt = new Option<string?>("--update-history", "Update issue view history (true|false)");
        var failFastOpt = new Option<string?>("--fail-fast", "Fail fast on invalid request details (true|false)");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        view.AddOption(verboseOpt);
        view.AddOption(fieldsOpt);
        view.AddOption(fieldsByKeysOpt);
        view.AddOption(expandOpt);
        view.AddOption(propertiesOpt);
        view.AddOption(updateHistoryOpt);
        view.AddOption(failFastOpt);
        view.AddOption(rawOpt);
        view.AddOption(failOnNonSuccessOpt);
        view.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
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
                parseResult.GetValueForOption(rawOpt),
                false,
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

        // Backward-compatible add form: issue comment <KEY> --text <TEXT>
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ZeroOrOne };
        var textOpt = new Option<string?>("--text", "Comment text for add/update");
        var bodyOpt = new Option<string?>("--body", "Inline JSON payload for Jira comment body");
        var bodyFileOpt = new Option<string?>("--body-file", "Read JSON payload from file path");
        var bodyAdfFileOpt = new Option<string?>("--body-adf-file", "Read comment body ADF JSON from file path");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        comment.AddArgument(keyArg);
        comment.AddOption(textOpt);
        comment.AddOption(bodyOpt);
        comment.AddOption(bodyFileOpt);
        comment.AddOption(bodyAdfFileOpt);
        comment.AddOption(expandOpt);
        comment.AddOption(rawOpt);
        comment.AddOption(failOnNonSuccessOpt);
        comment.AddOption(verboseOpt);
        comment.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            if (string.IsNullOrWhiteSpace(key))
            {
                Console.Error.WriteLine("Either use subcommands or provide <key> for the add form.");
                context.ExitCode = 1;
                return;
            }

            if (!TryBuildCommentBody(
                    parseResult.GetValueForOption(textOpt),
                    parseResult.GetValueForOption(bodyOpt),
                    parseResult.GetValueForOption(bodyFileOpt),
                    parseResult.GetValueForOption(bodyAdfFileOpt),
                    context,
                    out var body))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                $"/rest/api/3/issue/{key}/comment",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                body,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        comment.AddCommand(BuildCommentAddCommand(services));
        comment.AddCommand(BuildCommentListCommand(services));
        comment.AddCommand(BuildCommentGetCommand(services));
        comment.AddCommand(BuildCommentUpdateCommand(services));
        comment.AddCommand(BuildCommentDeleteCommand(services));

        return comment;
    }

    private static Command BuildCommentAddCommand(IServiceProvider services)
    {
        var add = new Command("add", "Add a comment to an issue");
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text", "Comment text");
        var bodyOpt = new Option<string?>("--body", "Inline JSON payload for Jira comment body");
        var bodyFileOpt = new Option<string?>("--body-file", "Read JSON payload from file path");
        var bodyAdfFileOpt = new Option<string?>("--body-adf-file", "Read comment body ADF JSON from file path");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        add.AddArgument(keyArg);
        add.AddOption(textOpt);
        add.AddOption(bodyOpt);
        add.AddOption(bodyFileOpt);
        add.AddOption(bodyAdfFileOpt);
        add.AddOption(expandOpt);
        add.AddOption(rawOpt);
        add.AddOption(failOnNonSuccessOpt);
        add.AddOption(verboseOpt);

        add.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            if (!TryBuildCommentBody(
                    parseResult.GetValueForOption(textOpt),
                    parseResult.GetValueForOption(bodyOpt),
                    parseResult.GetValueForOption(bodyFileOpt),
                    parseResult.GetValueForOption(bodyAdfFileOpt),
                    context,
                    out var resolvedBody))
            {
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
                resolvedBody,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return add;
    }

    private static Command BuildCommentListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List comments for an issue");
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum comments to return");
        var orderByOpt = new Option<string?>("--order-by", "Order by expression");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        list.AddArgument(keyArg);
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(orderByOpt);
        list.AddOption(expandOpt);
        list.AddOption(rawOpt);
        list.AddOption(failOnNonSuccessOpt);
        list.AddOption(verboseOpt);

        list.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            var startAt = parseResult.GetValueForOption(startAtOpt);
            if (startAt.HasValue && startAt.Value < 0)
            {
                Console.Error.WriteLine("--start-at must be zero or greater.");
                context.ExitCode = 1;
                return;
            }

            var maxResults = parseResult.GetValueForOption(maxResultsOpt);
            if (maxResults.HasValue && maxResults.Value <= 0)
            {
                Console.Error.WriteLine("--max-results must be greater than zero.");
                context.ExitCode = 1;
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
                parseResult.GetValueForOption(rawOpt),
                false,
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
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        get.AddArgument(keyArg);
        get.AddArgument(idArg);
        get.AddOption(expandOpt);
        get.AddOption(rawOpt);
        get.AddOption(failOnNonSuccessOpt);
        get.AddOption(verboseOpt);

        get.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
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
                parseResult.GetValueForOption(rawOpt),
                false,
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
        var update = new Command("update", "Update an existing issue comment");
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text", "Comment text");
        var bodyOpt = new Option<string?>("--body", "Inline JSON payload for Jira comment body");
        var bodyFileOpt = new Option<string?>("--body-file", "Read JSON payload from file path");
        var bodyAdfFileOpt = new Option<string?>("--body-adf-file", "Read comment body ADF JSON from file path");
        var notifyUsersOpt = new Option<string?>("--notify-users", "Notify users (true|false)");
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag", "Override editable flag (true|false)");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        update.AddArgument(keyArg);
        update.AddArgument(idArg);
        update.AddOption(textOpt);
        update.AddOption(bodyOpt);
        update.AddOption(bodyFileOpt);
        update.AddOption(bodyAdfFileOpt);
        update.AddOption(notifyUsersOpt);
        update.AddOption(overrideEditableFlagOpt);
        update.AddOption(expandOpt);
        update.AddOption(rawOpt);
        update.AddOption(failOnNonSuccessOpt);
        update.AddOption(verboseOpt);

        update.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            if (!TryBuildCommentBody(
                    parseResult.GetValueForOption(textOpt),
                    parseResult.GetValueForOption(bodyOpt),
                    parseResult.GetValueForOption(bodyFileOpt),
                    parseResult.GetValueForOption(bodyAdfFileOpt),
                    context,
                    out var resolvedBody))
            {
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
                resolvedBody,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return update;
    }

    private static Command BuildCommentDeleteCommand(IServiceProvider services)
    {
        var delete = new Command("delete", "Delete an issue comment");
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        delete.AddArgument(keyArg);
        delete.AddArgument(idArg);
        delete.AddOption(failOnNonSuccessOpt);
        delete.AddOption(verboseOpt);

        delete.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
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
                false,
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return delete;
    }

    private static Command BuildTransitionCommand(IServiceProvider services)
    {
        var transition = new Command("transition", "Issue transition commands");
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var toOpt = new Option<string?>("--to", "Target transition name (e.g. Done)");
        var idOpt = new Option<string?>("--id", "Target transition ID");
        var bodyOpt = new Option<string?>("--body", "Inline JSON payload for transition request");
        var bodyFileOpt = new Option<string?>("--body-file", "Read transition JSON payload from file path");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        transition.AddArgument(keyArg);
        transition.AddOption(toOpt);
        transition.AddOption(idOpt);
        transition.AddOption(bodyOpt);
        transition.AddOption(bodyFileOpt);
        transition.AddOption(rawOpt);
        transition.AddOption(failOnNonSuccessOpt);
        transition.AddOption(verboseOpt);
        transition.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var to = parseResult.GetValueForOption(toOpt);
            var id = parseResult.GetValueForOption(idOpt);
            if (!TryResolveBody(parseResult.GetValueForOption(bodyOpt), parseResult.GetValueForOption(bodyFileOpt), context, out var explicitBody))
            {
                return;
            }

            var hasTransitionSelector = !string.IsNullOrWhiteSpace(to) || !string.IsNullOrWhiteSpace(id);
            if (explicitBody is not null && hasTransitionSelector)
            {
                Console.Error.WriteLine("Use either --body/--body-file or --to/--id.");
                context.ExitCode = 1;
                return;
            }

            if (explicitBody is null && string.IsNullOrWhiteSpace(to) == string.IsNullOrWhiteSpace(id))
            {
                Console.Error.WriteLine("Provide exactly one of --to or --id.");
                context.ExitCode = 1;
                return;
            }

            string body;
            if (explicitBody is not null)
            {
                body = explicitBody;
            }
            else
            {
                var transitionId = id;
                if (string.IsNullOrWhiteSpace(transitionId))
                {
                    transitionId = await ResolveTransitionIdByNameAsync(
                        services,
                        config!,
                        key,
                        to!,
                        logger,
                        context.GetCancellationToken());
                    if (string.IsNullOrWhiteSpace(transitionId))
                    {
                        context.ExitCode = 1;
                        return;
                    }
                }

                var payload = new Dictionary<string, object?>
                {
                    ["transition"] = new Dictionary<string, string> { ["id"] = transitionId! }
                };
                body = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
            }

            var transitionOptions = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                $"/rest/api/3/issue/{key}/transitions",
                new List<KeyValuePair<string, string>>(),
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                body,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);
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
        var keyArg = new Argument<string>("key", "Issue key (e.g. TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var expandOpt = new Option<string?>("--expand", "Expand transition response entities");
        var transitionIdOpt = new Option<string?>("--transition-id", "Filter by transition ID");
        var skipRemoteOnlyConditionOpt = new Option<string?>("--skip-remote-only-condition", "Skip remote-only condition check (true|false)");
        var includeUnavailableTransitionsOpt = new Option<string?>("--include-unavailable-transitions", "Include unavailable transitions (true|false)");
        var sortByOpsBarAndStatusOpt = new Option<string?>("--sort-by-ops-bar-and-status", "Sort by ops bar and status (true|false)");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        list.AddArgument(keyArg);
        list.AddOption(expandOpt);
        list.AddOption(transitionIdOpt);
        list.AddOption(skipRemoteOnlyConditionOpt);
        list.AddOption(includeUnavailableTransitionsOpt);
        list.AddOption(sortByOpsBarAndStatusOpt);
        list.AddOption(rawOpt);
        list.AddOption(failOnNonSuccessOpt);
        list.AddOption(verboseOpt);

        list.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
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
                parseResult.GetValueForOption(rawOpt),
                false,
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

    private static bool TryResolveBody(string? body, string? bodyFile, InvocationContext context, out string? resolvedBody)
    {
        resolvedBody = null;

        if (!string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(bodyFile))
        {
            Console.Error.WriteLine("Use either --body or --body-file, not both.");
            context.ExitCode = 1;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(bodyFile))
        {
            try
            {
                resolvedBody = File.ReadAllText(bodyFile!);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read body file '{bodyFile}': {ex.Message}");
                context.ExitCode = 1;
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(body))
        {
            resolvedBody = body;
        }

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
            Console.Error.WriteLine($"Project mismatch: argument '{projectArgument}' does not match --project '{projectOption}'.");
            context.ExitCode = 1;
            return false;
        }

        return true;
    }

    private static bool TryResolveOptionalJsonObjectFile(
        string? filePath,
        string optionName,
        InvocationContext context,
        out JsonElement? jsonObject)
    {
        jsonObject = null;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return true;
        }

        if (!TryReadJsonObjectFile(filePath, optionName, context, out var parsed))
        {
            return false;
        }

        jsonObject = parsed;
        return true;
    }

    private static bool TryResolveNamedJsonObjectFile(
        string? name,
        string? filePath,
        string nameOptionName,
        string fileOptionName,
        InvocationContext context,
        out string? resolvedName,
        out JsonElement? jsonObject)
    {
        resolvedName = null;
        jsonObject = null;

        var hasName = !string.IsNullOrWhiteSpace(name);
        var hasFile = !string.IsNullOrWhiteSpace(filePath);
        if (!hasName && !hasFile)
        {
            return true;
        }

        if (!hasName || !hasFile)
        {
            Console.Error.WriteLine($"Use {nameOptionName} together with {fileOptionName}.");
            context.ExitCode = 1;
            return false;
        }

        resolvedName = name!.Trim();
        if (!TryReadJsonObjectFile(filePath!, fileOptionName, context, out var parsed))
        {
            return false;
        }

        jsonObject = parsed;
        return true;
    }

    private static bool TryReadJsonObjectFile(string filePath, string optionName, InvocationContext context, out JsonElement jsonObject)
    {
        jsonObject = default;
        try
        {
            var text = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                Console.Error.WriteLine($"{optionName} file '{filePath}' must contain a JSON object.");
                context.ExitCode = 1;
                return false;
            }

            jsonObject = doc.RootElement.Clone();
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read {optionName} file '{filePath}': {ex.Message}");
            context.ExitCode = 1;
            return false;
        }
    }

    private static bool TryBuildCommentBody(
        string? text,
        string? body,
        string? bodyFile,
        string? bodyAdfFile,
        InvocationContext context,
        out string? resolvedBody)
    {
        resolvedBody = null;

        if (!TryResolveBody(body, bodyFile, context, out var explicitBody))
        {
            return false;
        }

        if (!TryResolveOptionalJsonObjectFile(bodyAdfFile, "--body-adf-file", context, out var adfBody))
        {
            return false;
        }

        if (explicitBody is not null && adfBody.HasValue)
        {
            Console.Error.WriteLine("Use either --body/--body-file or --body-adf-file, not both.");
            context.ExitCode = 1;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(text) && (explicitBody is not null || adfBody.HasValue))
        {
            Console.Error.WriteLine("Use either --text or one of --body/--body-file/--body-adf-file, not both.");
            context.ExitCode = 1;
            return false;
        }

        if (explicitBody is not null)
        {
            resolvedBody = explicitBody;
            return true;
        }

        if (adfBody.HasValue)
        {
            resolvedBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["body"] = adfBody.Value });
            return true;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Console.Error.WriteLine("Provide --text, --body, --body-file, or --body-adf-file.");
            context.ExitCode = 1;
            return false;
        }

        var adfTextBody = new Dictionary<string, object?>
        {
            ["type"] = "doc",
            ["version"] = 1,
            ["content"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "paragraph",
                    ["content"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "text",
                            ["text"] = text
                        }
                    }
                }
            }
        };

        resolvedBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["body"] = adfTextBody });
        return true;
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

    private static Dictionary<string, object?> BuildFieldsDictionary(
        string? project,
        string? summary,
        string? issueType,
        object? description,
        string? assignee)
    {
        var fields = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(project))
        {
            fields["project"] = new Dictionary<string, string> { ["key"] = project };
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            fields["summary"] = summary;
        }

        if (!string.IsNullOrWhiteSpace(issueType))
        {
            fields["issuetype"] = new Dictionary<string, string> { ["name"] = issueType };
        }

        if (description is string descriptionText)
        {
            if (!string.IsNullOrWhiteSpace(descriptionText))
            {
                fields["description"] = descriptionText;
            }
        }
        else if (description is not null)
        {
            fields["description"] = description;
        }

        if (!string.IsNullOrWhiteSpace(assignee))
        {
            fields["assignee"] = new Dictionary<string, string> { ["accountId"] = assignee };
        }

        return fields;
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
            Console.Error.WriteLine($"{optionName} must be 'true' or 'false'.");
            context.ExitCode = 1;
            return false;
        }

        value = parsed;
        return true;
    }
}

