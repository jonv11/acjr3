using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class FieldCommands
{
    public static Command BuildFieldCommand(IServiceProvider services)
    {
        var field = new Command("field", "Jira field commands");
        field.AddCommand(BuildListCommand(services));
        field.AddCommand(BuildSearchCommand(services));
        return field;
    }

    private static Command BuildListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List all Jira fields");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
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

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/field",
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
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
        return list;
    }

    private static Command BuildSearchCommand(IServiceProvider services)
    {
        var search = new Command("search", "Search Jira fields");
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum number of fields to return");
        var typeOpt = new Option<string?>("--type", "Field type filter");
        var idOpt = new Option<string?>("--id", "Comma-separated field IDs");
        var queryOpt = new Option<string?>("--query", "Field search text");
        var orderByOpt = new Option<string?>("--order-by", "Order by expression");
        var expandOpt = new Option<string?>("--expand", "Expand related entities");
        var projectIdsOpt = new Option<string?>("--project-ids", "Comma-separated project IDs for project-scoped field search");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        search.AddOption(startAtOpt);
        search.AddOption(maxResultsOpt);
        search.AddOption(typeOpt);
        search.AddOption(idOpt);
        search.AddOption(queryOpt);
        search.AddOption(orderByOpt);
        search.AddOption(expandOpt);
        search.AddOption(projectIdsOpt);
        search.AddOption(failOnNonSuccessOpt);
        search.AddOption(verboseOpt);

        search.SetHandler(async (InvocationContext context) =>
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
            AddStringQuery(query, "type", parseResult.GetValueForOption(typeOpt));
            AddStringQuery(query, "id", parseResult.GetValueForOption(idOpt));
            AddStringQuery(query, "query", parseResult.GetValueForOption(queryOpt));
            AddStringQuery(query, "orderBy", parseResult.GetValueForOption(orderByOpt));
            AddStringQuery(query, "expand", parseResult.GetValueForOption(expandOpt));
            AddStringQuery(query, "projectIds", parseResult.GetValueForOption(projectIdsOpt));

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/field/search",
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





