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
        var keyArg = new Argument<string>("key") { Description = "Issue key (for example, TEST-123)", Arity = ArgumentArity.ExactlyOne };
        var startAtOpt = new Option<int?>("--start-at") { Description = "Pagination start index" };
        var maxResultsOpt = new Option<int?>("--max-results") { Description = "Maximum comments to return" };
        var orderByOpt = new Option<string?>("--order-by") { Description = "Order by expression" };
        var expandOpt = new Option<string?>("--expand") { Description = "Expand comment response entities" };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit" };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };

        list.AddArgument(keyArg);
        list.AddOption(startAtOpt);
        list.AddOption(maxResultsOpt);
        list.AddOption(orderByOpt);
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

    private static Command BuildCommentGetCommand(IServiceProvider services)
    {
        var get = new Command("get", "Get one comment from an issue");
        var keyArg = new Argument<string>("key") { Description = "Issue key (for example, TEST-123)", Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id") { Description = "Comment ID", Arity = ArgumentArity.ExactlyOne };
        var extractOpt = new Option<bool>("--extract") { Description = "Extract and return only the comment body JSON." };
        var expandOpt = new Option<string?>("--expand") { Description = "Expand comment response entities" };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit" };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };

        get.AddArgument(keyArg);
        get.AddArgument(idArg);
        get.AddOption(extractOpt);
        get.AddOption(expandOpt);
        get.AddOption(allowNonSuccessOpt);
        get.AddOption(verboseOpt);

        get.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var extract = parseResult.GetValueForOption(extractOpt);
            if (extract && !TryValidateExtractOutputOptions(outputPreferences, context))
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
                !parseResult.GetValueForOption(allowNonSuccessOpt),
                false,
                false,
                false,
                extract ? "body" : null);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return get;
    }

}



