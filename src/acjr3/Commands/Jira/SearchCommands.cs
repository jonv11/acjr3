using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class SearchCommands
{
    public static Command BuildSearchCommand(IServiceProvider services)
    {
        var search = new Command("search", "Jira issue search commands");
        search.AddCommand(BuildListCommand(services));
        return search;
    }

    private static Command BuildListCommand(IServiceProvider services)
    {
        var list = new Command("list", "Search issues using JQL or filters");
        var projectOpt = new Option<string>("--project", "Project key");
        var statusOpt = new Option<string>("--status", "Status filter");
        var assigneeOpt = new Option<string>("--assignee", "Assignee filter");
        var jqlOpt = new Option<string>("--jql", "Custom JQL query");
        var jqlFileOpt = new Option<string?>("--jql-file", "Read JQL text from file path");
        var fieldsOpt = new Option<string?>("--fields", "Comma-separated fields to return");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum results per page");
        var nextPageTokenOpt = new Option<string?>("--next-page-token", "Token for next page from previous search response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        list.AddOption(projectOpt);
        list.AddOption(statusOpt);
        list.AddOption(assigneeOpt);
        list.AddOption(jqlOpt);
        list.AddOption(jqlFileOpt);
        list.AddOption(fieldsOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(nextPageTokenOpt);
        list.AddOption(failOnNonSuccessOpt);
        list.AddOption(verboseOpt);
        list.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var project = parseResult.GetValueForOption(projectOpt);
            var status = parseResult.GetValueForOption(statusOpt);
            var assignee = parseResult.GetValueForOption(assigneeOpt);
            var jql = parseResult.GetValueForOption(jqlOpt);
            var jqlFile = parseResult.GetValueForOption(jqlFileOpt);

            string? jqlFromFile = null;
            if (!string.IsNullOrWhiteSpace(jqlFile))
            {
                try
                {
                    jqlFromFile = TextFileInput.ReadAllTextNormalized(jqlFile!);
                }
                catch (Exception ex)
                {
                    CliOutput.WriteValidationError(context, $"Failed to read JQL file '{jqlFile}': {ex.Message}");
                    return;
                }
            }

            var jqlParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(jql))
                jqlParts.Add(jql);
            if (!string.IsNullOrWhiteSpace(jqlFromFile))
                jqlParts.Add(jqlFromFile.Trim());
            if (!string.IsNullOrWhiteSpace(project))
                jqlParts.Add($"project = {project}");
            if (!string.IsNullOrWhiteSpace(status))
                jqlParts.Add($"status = '{status}'");
            if (!string.IsNullOrWhiteSpace(assignee))
                jqlParts.Add($"assignee = '{assignee}'");

            var maxResults = parseResult.GetValueForOption(maxResultsOpt);
            if (maxResults.HasValue && maxResults.Value <= 0)
            {
                CliOutput.WriteValidationError(context, "--max-results must be greater than zero.");
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            if (jqlParts.Count > 0)
                query.Add(new KeyValuePair<string, string>("jql", string.Join(" AND ", jqlParts)));

            query.Add(new KeyValuePair<string, string>("maxResults", (maxResults ?? 50).ToString()));

            var fields = parseResult.GetValueForOption(fieldsOpt);
            if (!string.IsNullOrWhiteSpace(fields))
                query.Add(new KeyValuePair<string, string>("fields", fields));

            var nextPageToken = parseResult.GetValueForOption(nextPageTokenOpt);
            if (!string.IsNullOrWhiteSpace(nextPageToken))
                query.Add(new KeyValuePair<string, string>("nextPageToken", nextPageToken));

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/search/jql",
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






