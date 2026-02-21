using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class UserCommands
{
    public static Command BuildUserCommand(IServiceProvider services)
    {
        var user = new Command("user", "Jira user commands");
        user.AddCommand(BuildSearchCommand(services));
        return user;
    }

    private static Command BuildSearchCommand(IServiceProvider services)
    {
        var search = new Command("search", "Search for users by name or email");
        var queryOpt = new Option<string?>("--query") { Description = "Search text (name or email)" };
        var usernameOpt = new Option<string?>("--username") { Description = "Legacy username search value" };
        var accountIdOpt = new Option<string?>("--account-id") { Description = "User accountId" };
        var startAtOpt = new Option<int?>("--start-at") { Description = "Pagination start index" };
        var maxResultsOpt = new Option<int?>("--max-results") { Description = "Maximum number of users to return" };
        var propertyOpt = new Option<string?>("--property") { Description = "User property query" };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit" };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };
        search.AddOption(queryOpt);
        search.AddOption(usernameOpt);
        search.AddOption(accountIdOpt);
        search.AddOption(startAtOpt);
        search.AddOption(maxResultsOpt);
        search.AddOption(propertyOpt);
        search.AddOption(allowNonSuccessOpt);
        search.AddOption(verboseOpt);
        search.SetHandler(async (InvocationContext context) =>
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

            var query = parseResult.GetValueForOption(queryOpt);
            var username = parseResult.GetValueForOption(usernameOpt);
            var accountId = parseResult.GetValueForOption(accountIdOpt);
            var property = parseResult.GetValueForOption(propertyOpt);
            if (string.IsNullOrWhiteSpace(query)
                && string.IsNullOrWhiteSpace(username)
                && string.IsNullOrWhiteSpace(accountId))
            {
                CliOutput.WriteValidationError(context, "Provide at least one of --query, --username, or --account-id.");
                return;
            }

            var queryParams = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddString(queryParams, "query", query);
            JiraQueryBuilder.AddString(queryParams, "username", username);
            JiraQueryBuilder.AddString(queryParams, "accountId", accountId);
            JiraQueryBuilder.AddInt(queryParams, "startAt", startAt);
            JiraQueryBuilder.AddInt(queryParams, "maxResults", maxResults);
            JiraQueryBuilder.AddString(queryParams, "property", property);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/user/search",
                queryParams,
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
        return search;
    }
}








