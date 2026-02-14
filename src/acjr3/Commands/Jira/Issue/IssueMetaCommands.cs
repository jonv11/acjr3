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
    private static Command BuildCreateMetaCommand(IServiceProvider services)
    {
        var createMeta = new Command("createmeta", "Get issue create metadata");
        var projectIdsOpt = new Option<string?>("--project-ids", "Comma-separated project IDs");
        var projectKeysOpt = new Option<string?>("--project-keys", "Comma-separated project keys");
        var issueTypeIdsOpt = new Option<string?>("--issuetype-ids", "Comma-separated issue type IDs");
        var issueTypeNamesOpt = new Option<string?>("--issuetype-names", "Comma-separated issue type names");
        var expandOpt = new Option<string?>("--expand", "Expand create metadata fields");
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success", "Allow 4xx/5xx responses without forcing a non-zero exit");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        createMeta.AddOption(projectIdsOpt);
        createMeta.AddOption(projectKeysOpt);
        createMeta.AddOption(issueTypeIdsOpt);
        createMeta.AddOption(issueTypeNamesOpt);
        createMeta.AddOption(expandOpt);
        createMeta.AddOption(allowNonSuccessOpt);
        createMeta.AddOption(verboseOpt);

        createMeta.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddString(query, "projectIds", parseResult.GetValueForOption(projectIdsOpt));
            JiraQueryBuilder.AddString(query, "projectKeys", parseResult.GetValueForOption(projectKeysOpt));
            JiraQueryBuilder.AddString(query, "issuetypeIds", parseResult.GetValueForOption(issueTypeIdsOpt));
            JiraQueryBuilder.AddString(query, "issuetypeNames", parseResult.GetValueForOption(issueTypeNamesOpt));
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));

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
                !parseResult.GetValueForOption(allowNonSuccessOpt),
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
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success", "Allow 4xx/5xx responses without forcing a non-zero exit");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        editMeta.AddArgument(keyArg);
        editMeta.AddOption(overrideScreenSecurityOpt);
        editMeta.AddOption(overrideEditableFlagOpt);
        editMeta.AddOption(allowNonSuccessOpt);
        editMeta.AddOption(verboseOpt);

        editMeta.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(overrideScreenSecurityOpt), "--override-screen-security", context, out var overrideScreenSecurity)
                || !TryParseBooleanOption(parseResult.GetValueForOption(overrideEditableFlagOpt), "--override-editable-flag", context, out var overrideEditableFlag))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddBoolean(query, "overrideScreenSecurity", overrideScreenSecurity);
            JiraQueryBuilder.AddBoolean(query, "overrideEditableFlag", overrideEditableFlag);

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
                !parseResult.GetValueForOption(allowNonSuccessOpt),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return editMeta;
    }

}


