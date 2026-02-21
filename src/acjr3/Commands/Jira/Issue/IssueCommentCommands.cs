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
    private static Command BuildCommentCommand(IServiceProvider services)
    {
        var comment = new Command("comment", "Issue comment commands");
        comment.AddCommand(BuildCommentAddCommand(services));
        comment.AddCommand(BuildCommentListCommand(services));
        comment.AddCommand(BuildCommentGetCommand(services));
        comment.AddCommand(BuildCommentUpdateCommand(services));
        comment.AddCommand(BuildCommentDeleteCommand(services));

        return comment;
    }

    private static Command BuildCommentAddCommand(IServiceProvider services)
    {
        var add = new Command("add", "Add a comment to an issue (POST /rest/api/3/issue/{issueIdOrKey}/comment). Starts from a default payload, optional --in base payload, then applies sugar flags.");
        var keyArg = new Argument<string>("key") { Description = "Issue key (for example, TEST-123)", Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text") { Description = "Comment text" };
        var textFileOpt = new Option<string?>("--text-file") { Description = "Read comment body node from file path" };
        var textFormatOpt = new Option<string>("--text-format") { DefaultValueFactory = _ => "adf", Description = "Comment text file format: json|adf" };
        var inOpt = new Option<string?>("--in") { Description = "Path to request payload file, or '-' for stdin." };
        var expandOpt = new Option<string?>("--expand") { Description = "Expand comment response entities" };
        var yesOpt = new Option<bool>("--yes") { Description = "Confirm mutating operations." };
        var forceOpt = new Option<bool>("--force") { Description = "Force mutating operations." };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit." };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };

        add.AddArgument(keyArg);
        add.AddOption(textOpt);
        add.AddOption(textFileOpt);
        add.AddOption(textFormatOpt);
        add.AddOption(inOpt);
        add.AddOption(expandOpt);
        add.AddOption(yesOpt);
        add.AddOption(forceOpt);
        add.AddOption(allowNonSuccessOpt);
        add.AddOption(verboseOpt);

        add.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var addCommentBasePayload = await TryResolveCommentBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                context,
                context.GetCancellationToken());
            if (!addCommentBasePayload.Ok)
            {
                return;
            }
            var payloadObject = addCommentBasePayload.Payload!;

            if (!TryResolveCommentBodyValue(
                    parseResult.GetValueForOption(textOpt),
                    parseResult.GetValueForOption(textFileOpt),
                    parseResult.GetValueForOption(textFormatOpt),
                    WasOptionSupplied(parseResult, "--text-format"),
                    WasOptionSupplied(parseResult, "--text"),
                    context,
                    out var resolvedBodyValue))
            {
                return;
            }

            if (resolvedBodyValue is not null)
            {
                JsonPayloadPipeline.SetNode(payloadObject, resolvedBodyValue, "body");
            }

            if (!HasValidCommentBody(payloadObject))
            {
                CliOutput.WriteValidationError(context, "Final payload must include a non-empty body.");
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                $"/rest/api/3/issue/{key}/comment",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                JsonPayloadPipeline.Serialize(payloadObject),
                null,
                outputPreferences,
                !parseResult.GetValueForOption(allowNonSuccessOpt),
                false,
                false,
                parseResult.GetValueForOption(yesOpt) || parseResult.GetValueForOption(forceOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return add;
    }

    private static Command BuildCommentUpdateCommand(IServiceProvider services)
    {
        var update = new Command("update", "Update an issue comment (PUT /rest/api/3/issue/{issueIdOrKey}/comment/{id}). Starts from a default payload, optional --in base payload, then applies sugar flags.");
        var keyArg = new Argument<string>("key") { Description = "Issue key (for example, TEST-123)", Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id") { Description = "Comment ID", Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text") { Description = "Comment text" };
        var textFileOpt = new Option<string?>("--text-file") { Description = "Read comment body node from file path" };
        var textFormatOpt = new Option<string>("--text-format") { DefaultValueFactory = _ => "adf", Description = "Comment text file format: json|adf" };
        var inOpt = new Option<string?>("--in") { Description = "Path to request payload file, or '-' for stdin." };
        var notifyUsersOpt = new Option<string?>("--notify-users") { Description = "Notify users (true|false)" };
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag") { Description = "Override editable flag (true|false)" };
        var expandOpt = new Option<string?>("--expand") { Description = "Expand comment response entities" };
        var yesOpt = new Option<bool>("--yes") { Description = "Confirm mutating operations." };
        var forceOpt = new Option<bool>("--force") { Description = "Force mutating operations." };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit." };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };

        update.AddArgument(keyArg);
        update.AddArgument(idArg);
        update.AddOption(textOpt);
        update.AddOption(textFileOpt);
        update.AddOption(textFormatOpt);
        update.AddOption(inOpt);
        update.AddOption(notifyUsersOpt);
        update.AddOption(overrideEditableFlagOpt);
        update.AddOption(expandOpt);
        update.AddOption(yesOpt);
        update.AddOption(forceOpt);
        update.AddOption(allowNonSuccessOpt);
        update.AddOption(verboseOpt);

        update.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var updateCommentBasePayload = await TryResolveCommentBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                context,
                context.GetCancellationToken());
            if (!updateCommentBasePayload.Ok)
            {
                return;
            }
            var payloadObject = updateCommentBasePayload.Payload!;

            if (!TryResolveCommentBodyValue(
                    parseResult.GetValueForOption(textOpt),
                    parseResult.GetValueForOption(textFileOpt),
                    parseResult.GetValueForOption(textFormatOpt),
                    WasOptionSupplied(parseResult, "--text-format"),
                    WasOptionSupplied(parseResult, "--text"),
                    context,
                    out var resolvedBodyValue))
            {
                return;
            }

            if (resolvedBodyValue is not null)
            {
                JsonPayloadPipeline.SetNode(payloadObject, resolvedBodyValue, "body");
            }

            if (!HasValidCommentBody(payloadObject))
            {
                CliOutput.WriteValidationError(context, "Final payload must include a non-empty body.");
                return;
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(notifyUsersOpt), "--notify-users", context, out var notifyUsers)
                || !TryParseBooleanOption(parseResult.GetValueForOption(overrideEditableFlagOpt), "--override-editable-flag", context, out var overrideEditableFlag))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddBoolean(query, "notifyUsers", notifyUsers);
            JiraQueryBuilder.AddBoolean(query, "overrideEditableFlag", overrideEditableFlag);
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));

            var key = parseResult.GetValueForArgument(keyArg);
            var id = parseResult.GetValueForArgument(idArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Put,
                $"/rest/api/3/issue/{key}/comment/{id}",
                query,
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                JsonPayloadPipeline.Serialize(payloadObject),
                null,
                outputPreferences,
                !parseResult.GetValueForOption(allowNonSuccessOpt),
                false,
                false,
                parseResult.GetValueForOption(yesOpt) || parseResult.GetValueForOption(forceOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return update;
    }

    private static Command BuildCommentDeleteCommand(IServiceProvider services)
    {
        var delete = new Command("delete", "Delete an issue comment (DELETE /rest/api/3/issue/{issueIdOrKey}/comment/{id}).");
        var keyArg = new Argument<string>("key") { Description = "Issue key (for example, TEST-123)", Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id") { Description = "Comment ID", Arity = ArgumentArity.ExactlyOne };
        var yesOpt = new Option<bool>("--yes") { Description = "Confirm mutating operations." };
        var forceOpt = new Option<bool>("--force") { Description = "Force mutating operations." };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit." };
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };

        delete.AddArgument(keyArg);
        delete.AddArgument(idArg);
        delete.AddOption(yesOpt);
        delete.AddOption(forceOpt);
        delete.AddOption(allowNonSuccessOpt);
        delete.AddOption(verboseOpt);

        delete.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var id = parseResult.GetValueForArgument(idArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Delete,
                $"/rest/api/3/issue/{key}/comment/{id}",
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
                parseResult.GetValueForOption(yesOpt) || parseResult.GetValueForOption(forceOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return delete;
    }
}


