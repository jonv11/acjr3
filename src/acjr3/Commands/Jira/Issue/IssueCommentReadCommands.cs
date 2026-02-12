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
    private static Command BuildCommentListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List comments for an issue");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var startAtOpt = new Option<int?>("--start-at", "Pagination start index");
        var maxResultsOpt = new Option<int?>("--max-results", "Maximum comments to return");
        var orderByOpt = new Option<string?>("--order-by", "Order by expression");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        list.AddArgument(keyArg);
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(orderByOpt);
        list.AddOption(expandOpt);
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
            JiraQueryBuilder.AddString(query, "orderBy", parseResult.GetValueForOption(orderByOpt));
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}/comment",
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

    private static Command BuildCommentGetCommand(IServiceProvider services)
    {
        var get = new Command("get", "Get one comment from an issue");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        get.AddArgument(keyArg);
        get.AddArgument(idArg);
        get.AddOption(expandOpt);
        get.AddOption(failOnNonSuccessOpt);
        get.AddOption(verboseOpt);

        get.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var id = parseResult.GetValueForArgument(idArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}/comment/{id}",
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

        return get;
    }

}
