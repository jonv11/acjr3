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
        var projectOpt = new Option<string>("--project") { Description = "Project key", Required = true };
        var startAtOpt = new Option<int?>("--start-at") { Description = "Pagination start index" };
        var maxResultsOpt = new Option<int?>("--max-results") { Description = "Maximum number of versions to return" };
        var orderByOpt = new Option<string?>("--order-by") { Description = "Order by expression" };
        var queryOpt = new Option<string?>("--query") { Description = "Filter versions by text" };
        var statusOpt = new Option<string?>("--status") { Description = "Version status filter" };
        var expandOpt = new Option<string?>("--expand") { Description = "Expand related entities" };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit" };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };
        list.AddOption(projectOpt);
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(orderByOpt);
        list.AddOption(queryOpt);
        list.AddOption(statusOpt);
        list.AddOption(expandOpt);
        list.AddOption(allowNonSuccessOpt);
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

            var projectKey = parseResult.GetValueForOption(projectOpt);
            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddInt(query, "startAt", startAt);
            JiraQueryBuilder.AddInt(query, "maxResults", maxResults);
            JiraQueryBuilder.AddString(query, "orderBy", parseResult.GetValueForOption(orderByOpt));
            JiraQueryBuilder.AddString(query, "query", parseResult.GetValueForOption(queryOpt));
            JiraQueryBuilder.AddString(query, "status", parseResult.GetValueForOption(statusOpt));
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));

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
                !parseResult.GetValueForOption(allowNonSuccessOpt),
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
        var startAtOpt = new Option<int?>("--start-at") { Description = "Pagination start index" };
        var maxResultsOpt = new Option<int?>("--max-results") { Description = "Maximum number of projects to return" };
        var orderByOpt = new Option<string?>("--order-by") { Description = "Order by expression" };
        var idOpt = new Option<string?>("--id") { Description = "Comma-separated project IDs" };
        var keysOpt = new Option<string?>("--keys") { Description = "Comma-separated project keys" };
        var queryOpt = new Option<string?>("--query") { Description = "Project query text" };
        var typeKeyOpt = new Option<string?>("--type-key") { Description = "Project type key" };
        var categoryIdOpt = new Option<int?>("--category-id") { Description = "Project category ID" };
        var actionOpt = new Option<string?>("--action") { Description = "Action scope filter" };
        var expandOpt = new Option<string?>("--expand") { Description = "Expand related entities" };
        var statusOpt = new Option<string?>("--status") { Description = "Project status filter" };
        var propertiesOpt = new Option<string?>("--properties") { Description = "Comma-separated project properties" };
        var propertyQueryOpt = new Option<string?>("--property-query") { Description = "Property query string" };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit" };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };
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
        list.AddOption(allowNonSuccessOpt);
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

            var categoryId = parseResult.GetValueForOption(categoryIdOpt);
            if (categoryId.HasValue && categoryId.Value < 0)
            {
                CliOutput.WriteValidationError(context, "--category-id must be zero or greater.");
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddInt(query, "startAt", startAt);
            JiraQueryBuilder.AddInt(query, "maxResults", maxResults);
            JiraQueryBuilder.AddString(query, "orderBy", parseResult.GetValueForOption(orderByOpt));
            JiraQueryBuilder.AddString(query, "id", parseResult.GetValueForOption(idOpt));
            JiraQueryBuilder.AddString(query, "keys", parseResult.GetValueForOption(keysOpt));
            JiraQueryBuilder.AddString(query, "query", parseResult.GetValueForOption(queryOpt));
            JiraQueryBuilder.AddString(query, "typeKey", parseResult.GetValueForOption(typeKeyOpt));
            JiraQueryBuilder.AddInt(query, "categoryId", categoryId);
            JiraQueryBuilder.AddString(query, "action", parseResult.GetValueForOption(actionOpt));
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));
            JiraQueryBuilder.AddString(query, "status", parseResult.GetValueForOption(statusOpt));
            JiraQueryBuilder.AddString(query, "properties", parseResult.GetValueForOption(propertiesOpt));
            JiraQueryBuilder.AddString(query, "propertyQuery", parseResult.GetValueForOption(propertyQueryOpt));

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
                !parseResult.GetValueForOption(allowNonSuccessOpt),
                false,
                false,
                false);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
        return list;
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
        var projectOpt = new Option<string>("--project") { Description = "Project key", Required = true };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit" };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };
        list.AddOption(projectOpt);
        list.AddOption(allowNonSuccessOpt);
        list.AddOption(verboseOpt);
        list.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
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
                !parseResult.GetValueForOption(allowNonSuccessOpt),
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









