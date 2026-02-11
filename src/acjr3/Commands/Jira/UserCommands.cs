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
        var queryOpt = new Option<string?>("--query", "Search text (name or email)");
        var usernameOpt = new Option<string?>("--username", "Legacy username search value");
        var accountIdOpt = new Option<string?>("--account-id", "User accountId");
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum number of users to return");
        var propertyOpt = new Option<string?>("--property", "User property query");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        search.AddOption(queryOpt);
        search.AddOption(usernameOpt);
        search.AddOption(accountIdOpt);
        search.AddOption(startAtOpt);
        search.AddOption(maxResultsOpt);
        search.AddOption(propertyOpt);
        search.AddOption(rawOpt);
        search.AddOption(failOnNonSuccessOpt);
        search.AddOption(verboseOpt);
        search.SetHandler(async (InvocationContext context) =>
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

            var query = parseResult.GetValueForOption(queryOpt);
            var username = parseResult.GetValueForOption(usernameOpt);
            var accountId = parseResult.GetValueForOption(accountIdOpt);
            var property = parseResult.GetValueForOption(propertyOpt);
            if (string.IsNullOrWhiteSpace(query)
                && string.IsNullOrWhiteSpace(username)
                && string.IsNullOrWhiteSpace(accountId))
            {
                Console.Error.WriteLine("Provide at least one of --query, --username, or --account-id.");
                context.ExitCode = 1;
                return;
            }

            var queryParams = new List<KeyValuePair<string, string>>();
            AddStringQuery(queryParams, "query", query);
            AddStringQuery(queryParams, "username", username);
            AddStringQuery(queryParams, "accountId", accountId);
            AddIntQuery(queryParams, "startAt", startAt);
            AddIntQuery(queryParams, "maxResults", maxResults);
            AddStringQuery(queryParams, "property", property);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/user/search",
                queryParams,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                null,
                null,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                parseResult.GetValueForOption(failOnNonSuccessOpt),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
        return search;
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
}


