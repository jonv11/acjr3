using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class ResolutionCommands
{
    public static Command BuildResolutionCommand(IServiceProvider services)
    {
        var resolution = new Command("resolution", "Jira resolution commands");
        resolution.AddCommand(BuildListCommand(services));
        return resolution;
    }

    private static Command BuildListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List all Jira resolutions");
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum number of resolutions to return");
        var idOpt = new Option<string?>("--id", "Comma-separated resolution IDs");
        var onlyDefaultOpt = new Option<string?>("--only-default", "Filter default resolutions only (true|false)");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(idOpt);
        list.AddOption(onlyDefaultOpt);
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

            var onlyDefaultRaw = parseResult.GetValueForOption(onlyDefaultOpt);
            if (!TryParseBooleanOption(onlyDefaultRaw, "--only-default", context, out var onlyDefault))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddInt(query, "startAt", startAt);
            JiraQueryBuilder.AddInt(query, "maxResults", maxResults);
            JiraQueryBuilder.AddString(query, "id", parseResult.GetValueForOption(idOpt));
            if (onlyDefault.HasValue)
            {
                JiraQueryBuilder.AddBoolean(query, "onlyDefault", onlyDefault);
            }

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                "/rest/api/3/resolution/search",
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






