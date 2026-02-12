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
    private static Command BuildDeleteCommand(IServiceProvider services)
    {
        var delete = new Command("delete", "Delete a Jira issue (DELETE /rest/api/3/issue/{issueIdOrKey}).");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var deleteSubtasksOpt = new Option<string?>("--delete-subtasks", "Jira query parameter deleteSubtasks=true|false");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        delete.AddArgument(keyArg);
        delete.AddOption(deleteSubtasksOpt);
        delete.AddOption(yesOpt);
        delete.AddOption(forceOpt);
        delete.AddOption(failOnNonSuccessOpt);
        delete.AddOption(verboseOpt);

        delete.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            if (!TryParseBooleanOption(parseResult.GetValueForOption(deleteSubtasksOpt), "--delete-subtasks", context, out var deleteSubtasks))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddBoolean(query, "deleteSubtasks", deleteSubtasks);

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Delete,
                $"/rest/api/3/issue/{key}",
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
                parseResult.GetValueForOption(yesOpt) || parseResult.GetValueForOption(forceOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return delete;
    }
}

