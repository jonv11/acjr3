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
        var add = new Command("add", "Add a comment to an issue (POST /rest/api/3/issue/{issueIdOrKey}/comment). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text", "Comment text");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        add.AddArgument(keyArg);
        add.AddOption(textOpt);
        add.AddOption(bodyOpt);
        add.AddOption(bodyFileOpt);
        add.AddOption(inOpt);
        add.AddOption(inputFormatOpt);
        add.AddOption(expandOpt);
        add.AddOption(yesOpt);
        add.AddOption(forceOpt);
        add.AddOption(failOnNonSuccessOpt);
        add.AddOption(verboseOpt);

        add.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            if (!InputResolver.TryParseFormat(parseResult.GetValueForOption(inputFormatOpt), out var inputFormat, out var formatError))
            {
                CliOutput.WriteValidationError(context, formatError);
                return;
            }

            if (!InputResolver.TryResolveExplicitPayloadSource(
                    parseResult.GetValueForOption(inOpt),
                    parseResult.GetValueForOption(bodyOpt),
                    parseResult.GetValueForOption(bodyFileOpt),
                    out var payloadSource,
                    out var sourceError))
            {
                CliOutput.WriteValidationError(context, sourceError);
                return;
            }

            var addCommentBasePayload = await TryResolveCommentBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                parseResult.GetValueForOption(bodyOpt),
                parseResult.GetValueForOption(bodyFileOpt),
                inputFormat,
                payloadSource,
                context,
                context.GetCancellationToken());
            if (!addCommentBasePayload.Ok)
            {
                return;
            }
            var payloadObject = addCommentBasePayload.Payload!;

            var text = parseResult.GetValueForOption(textOpt);
            if (!string.IsNullOrWhiteSpace(text))
            {
                JsonPayloadPipeline.SetNode(payloadObject, BuildCommentAdfTextNode(text), "body");
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
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
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
        var update = new Command("update", "Update an issue comment (PUT /rest/api/3/issue/{issueIdOrKey}/comment/{id}). Starts from a default payload, optional explicit base (--body/--body-file/--in), then applies sugar flags.");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var textOpt = new Option<string?>("--text", "Comment text");
        var bodyOpt = new Option<string?>("--body", "Inline JSON base payload (JSON object).");
        var bodyFileOpt = new Option<string?>("--body-file", "Path to JSON base payload file (JSON object).");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var inputFormatOpt = new Option<string>("--input-format", () => "json", "Input format: json|adf|md|text.");
        var notifyUsersOpt = new Option<string?>("--notify-users", "Notify users (true|false)");
        var overrideEditableFlagOpt = new Option<string?>("--override-editable-flag", "Override editable flag (true|false)");
        var expandOpt = new Option<string?>("--expand", "Expand comment response entities");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        update.AddArgument(keyArg);
        update.AddArgument(idArg);
        update.AddOption(textOpt);
        update.AddOption(bodyOpt);
        update.AddOption(bodyFileOpt);
        update.AddOption(inOpt);
        update.AddOption(inputFormatOpt);
        update.AddOption(notifyUsersOpt);
        update.AddOption(overrideEditableFlagOpt);
        update.AddOption(expandOpt);
        update.AddOption(yesOpt);
        update.AddOption(forceOpt);
        update.AddOption(failOnNonSuccessOpt);
        update.AddOption(verboseOpt);

        update.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            if (!InputResolver.TryParseFormat(parseResult.GetValueForOption(inputFormatOpt), out var inputFormat, out var formatError))
            {
                CliOutput.WriteValidationError(context, formatError);
                return;
            }

            if (!InputResolver.TryResolveExplicitPayloadSource(
                    parseResult.GetValueForOption(inOpt),
                    parseResult.GetValueForOption(bodyOpt),
                    parseResult.GetValueForOption(bodyFileOpt),
                    out var payloadSource,
                    out var sourceError))
            {
                CliOutput.WriteValidationError(context, sourceError);
                return;
            }

            var updateCommentBasePayload = await TryResolveCommentBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                parseResult.GetValueForOption(bodyOpt),
                parseResult.GetValueForOption(bodyFileOpt),
                inputFormat,
                payloadSource,
                context,
                context.GetCancellationToken());
            if (!updateCommentBasePayload.Ok)
            {
                return;
            }
            var payloadObject = updateCommentBasePayload.Payload!;

            var text = parseResult.GetValueForOption(textOpt);
            if (!string.IsNullOrWhiteSpace(text))
            {
                JsonPayloadPipeline.SetNode(payloadObject, BuildCommentAdfTextNode(text), "body");
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
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
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
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var idArg = new Argument<string>("id", "Comment ID") { Arity = ArgumentArity.ExactlyOne };
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        delete.AddArgument(keyArg);
        delete.AddArgument(idArg);
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
