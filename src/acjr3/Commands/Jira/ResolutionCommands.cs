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
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(idOpt);
        list.AddOption(onlyDefaultOpt);
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

            var onlyDefaultRaw = parseResult.GetValueForOption(onlyDefaultOpt);
            if (!TryParseBooleanOption(onlyDefaultRaw, "--only-default", context, out var onlyDefault))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            AddQueryInt(query, "startAt", startAt);
            AddQueryInt(query, "maxResults", maxResults);
            AddQueryString(query, "id", parseResult.GetValueForOption(idOpt));
            if (onlyDefault.HasValue)
            {
                query.Add(new KeyValuePair<string, string>("onlyDefault", onlyDefault.Value ? "true" : "false"));
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
            Console.Error.WriteLine($"{optionName} must be 'true' or 'false'.");
            context.ExitCode = 1;
            return false;
        }

        value = parsed;
        return true;
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
}


