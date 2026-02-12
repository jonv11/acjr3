using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class ProjectCommands
{
    public static Command BuildProjectCommand(IServiceProvider services)
    {
        var project = new Command("project", "Jira project commands");
        project.AddCommand(BuildListCommand(services));
        project.AddCommand(BuildComponentCommand(services));
        project.AddCommand(BuildVersionCommand(services));
        return project;

    }

    private static Command BuildVersionCommand(IServiceProvider services)
    {
        var version = new Command("version", "Version-related commands for a project");
        version.AddCommand(BuildVersionListCommand(services));
        return version;
    }

    private static Command BuildVersionListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List all versions for a project");
        var projectOpt = new Option<string>("--project", "Project key") { IsRequired = true };
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum number of versions to return");
        var orderByOpt = new Option<string?>("--order-by", "Order by expression");
        var queryOpt = new Option<string?>("--query", "Filter versions by text");
        var statusOpt = new Option<string?>("--status", "Version status filter");
        var expandOpt = new Option<string?>("--expand", "Expand related entities");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        list.AddOption(projectOpt);
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(orderByOpt);
        list.AddOption(queryOpt);
        list.AddOption(statusOpt);
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

            var projectKey = parseResult.GetValueForOption(projectOpt);
            var query = new List<KeyValuePair<string, string>>();
            AddQueryInt(query, "startAt", startAt);
            AddQueryInt(query, "maxResults", maxResults);
            AddQueryString(query, "orderBy", parseResult.GetValueForOption(orderByOpt));
            AddQueryString(query, "query", parseResult.GetValueForOption(queryOpt));
            AddQueryString(query, "status", parseResult.GetValueForOption(statusOpt));
            AddQueryString(query, "expand", parseResult.GetValueForOption(expandOpt));

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/project/{projectKey}/version",
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

    private static Command BuildListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List all Jira projects");
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum number of projects to return");
        var orderByOpt = new Option<string?>("--order-by", "Order by expression");
        var idOpt = new Option<string?>("--id", "Comma-separated project IDs");
        var keysOpt = new Option<string?>("--keys", "Comma-separated project keys");
        var queryOpt = new Option<string?>("--query", "Project query text");
        var typeKeyOpt = new Option<string?>("--type-key", "Project type key");
        var categoryIdOpt = new Option<int?>("--category-id", "Project category ID");
        var actionOpt = new Option<string?>("--action", "Action scope filter");
        var expandOpt = new Option<string?>("--expand", "Expand related entities");
        var statusOpt = new Option<string?>("--status", "Project status filter");
        var propertiesOpt = new Option<string?>("--properties", "Comma-separated project properties");
        var propertyQueryOpt = new Option<string?>("--property-query", "Property query string");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(orderByOpt);
        list.AddOption(idOpt);
        list.AddOption(keysOpt);
        list.AddOption(queryOpt);
        list.AddOption(typeKeyOpt);
        list.AddOption(categoryIdOpt);
        list.AddOption(actionOpt);
        list.AddOption(expandOpt);
        list.AddOption(statusOpt);
        list.AddOption(propertiesOpt);
        list.AddOption(propertyQueryOpt);
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

            var categoryId = parseResult.GetValueForOption(categoryIdOpt);
            if (categoryId.HasValue && categoryId.Value < 0)
            {
                CliOutput.WriteValidationError(context, "--category-id must be zero or greater.");
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddQueryInt(query, "startAt", startAt);
            AddQueryInt(query, "maxResults", maxResults);
            AddQueryString(query, "orderBy", parseResult.GetValueForOption(orderByOpt));
            AddQueryString(query, "id", parseResult.GetValueForOption(idOpt));
            AddQueryString(query, "keys", parseResult.GetValueForOption(keysOpt));
            AddQueryString(query, "query", parseResult.GetValueForOption(queryOpt));
            AddQueryString(query, "typeKey", parseResult.GetValueForOption(typeKeyOpt));
            AddQueryInt(query, "categoryId", categoryId);
            AddQueryString(query, "action", parseResult.GetValueForOption(actionOpt));
            AddQueryString(query, "expand", parseResult.GetValueForOption(expandOpt));
            AddQueryString(query, "status", parseResult.GetValueForOption(statusOpt));
            AddQueryString(query, "properties", parseResult.GetValueForOption(propertiesOpt));
            AddQueryString(query, "propertyQuery", parseResult.GetValueForOption(propertyQueryOpt));

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/project/search",
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

    private static void AddQueryString(List<KeyValuePair<string, string>> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    private static void AddQueryInt(List<KeyValuePair<string, string>> query, string key, int? value)
    {
        if (value.HasValue)
        {
            query.Add(new KeyValuePair<string, string>(key, value.Value.ToString()));
        }
    }

    private static Command BuildComponentCommand(IServiceProvider services)
    {
        var component = new Command("component", "Component-related commands for a project");
        component.AddCommand(BuildComponentListCommand(services));
        return component;
    }

    private static Command BuildComponentListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List all components for a project");
        var projectOpt = new Option<string>("--project", "Project key") { IsRequired = true };
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        list.AddOption(projectOpt);
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
            var projectKey = parseResult.GetValueForOption(projectOpt);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/project/{projectKey}/components",
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
}





