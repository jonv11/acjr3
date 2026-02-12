using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class GroupCommands
{
    public static Command BuildGroupCommand(IServiceProvider services)
    {
        var group = new Command("group", "Jira group commands");
        group.AddCommand(BuildListCommand(services));
        return group;
    }

    private static Command BuildListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List all Jira groups");
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum number of groups to return");
        var groupIdOpt = new Option<string?>("--group-id", "Filter by group ID");
        var groupNameOpt = new Option<string?>("--group-name", "Filter by group name");
        var accessTypeOpt = new Option<string?>("--access-type", "Filter by access type");
        var applicationKeyOpt = new Option<string?>("--application-key", "Filter by application key");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(groupIdOpt);
        list.AddOption(groupNameOpt);
        list.AddOption(accessTypeOpt);
        list.AddOption(applicationKeyOpt);
        list.AddOption(failOnNonSuccessOpt);
        list.AddOption(verboseOpt);
        list.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
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
            JiraQueryBuilder.AddInt(query, "startAt", startAt);
            JiraQueryBuilder.AddInt(query, "maxResults", maxResults);
            JiraQueryBuilder.AddString(query, "groupId", parseResult.GetValueForOption(groupIdOpt));
            JiraQueryBuilder.AddString(query, "groupName", parseResult.GetValueForOption(groupNameOpt));
            JiraQueryBuilder.AddString(query, "accessType", parseResult.GetValueForOption(accessTypeOpt));
            JiraQueryBuilder.AddString(query, "applicationKey", parseResult.GetValueForOption(applicationKeyOpt));

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/group/bulk",
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
}






