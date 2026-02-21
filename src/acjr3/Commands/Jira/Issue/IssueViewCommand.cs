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
    private static Command BuildViewCommand(IServiceProvider services)
    {
        var view = new Command("view", "Show details for a specific issue");
        var keyArg = new Argument<string>("key") { Description = "Issue key (for example, TEST-123)", Arity = ArgumentArity.ExactlyOne };
        view.AddArgument(keyArg);
        var verboseOpt = new Option<bool>("--verbose") { Description = "Enable verbose diagnostics logging" };
        var fieldsOpt = new Option<string?>("--fields") { Description = "Comma-separated list of fields to include in the response (for example, summary,description)." };
        var extractOpt = new Option<string?>("--extract") { Description = "Extract and return only one issue field value as JSON (fields.<fieldName>)." };
        var fieldsByKeysOpt = new Option<string?>("--fields-by-keys") { Description = "Interpret fields in --fields by key (true|false)" };
        var expandOpt = new Option<string?>("--expand") { Description = "Expand issue response entities" };
        var propertiesOpt = new Option<string?>("--properties") { Description = "Comma-separated issue properties to include" };
        var updateHistoryOpt = new Option<string?>("--update-history") { Description = "Update issue view history (true|false)" };
        var failFastOpt = new Option<string?>("--fail-fast") { Description = "Fail fast on invalid request details (true|false)" };
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success") { Description = "Allow 4xx/5xx responses without forcing a non-zero exit" };
        view.AddOption(verboseOpt);
        view.AddOption(fieldsOpt);
        view.AddOption(extractOpt);
        view.AddOption(fieldsByKeysOpt);
        view.AddOption(expandOpt);
        view.AddOption(propertiesOpt);
        view.AddOption(updateHistoryOpt);
        view.AddOption(failFastOpt);
        view.AddOption(allowNonSuccessOpt);
        view.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var fields = parseResult.GetValueForOption(fieldsOpt);
            var extractFieldRaw = parseResult.GetValueForOption(extractOpt);
            var extractSupplied = WasOptionSupplied(parseResult, "--extract");
            string? extractField = null;
            if (extractSupplied)
            {
                if (string.IsNullOrWhiteSpace(extractFieldRaw))
                {
                    CliOutput.WriteValidationError(context, "--extract requires a non-empty field name.");
                    return;
                }

                if (!TryValidateExtractOutputOptions(outputPreferences, context))
                {
                    return;
                }

                extractField = extractFieldRaw.Trim();
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(fieldsByKeysOpt), "--fields-by-keys", context, out var fieldsByKeys)
                || !TryParseBooleanOption(parseResult.GetValueForOption(updateHistoryOpt), "--update-history", context, out var updateHistory)
                || !TryParseBooleanOption(parseResult.GetValueForOption(failFastOpt), "--fail-fast", context, out var failFast))
            {
                return;
            }

            var queryParams = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrWhiteSpace(fields))
            {
                queryParams.Add(new KeyValuePair<string, string>("fields", fields));
            }
            else if (extractSupplied && !WasOptionSupplied(parseResult, "--fields"))
            {
                queryParams.Add(new KeyValuePair<string, string>("fields", extractField!));
            }

            JiraQueryBuilder.AddBoolean(queryParams, "fieldsByKeys", fieldsByKeys);
            JiraQueryBuilder.AddString(queryParams, "expand", parseResult.GetValueForOption(expandOpt));
            JiraQueryBuilder.AddString(queryParams, "properties", parseResult.GetValueForOption(propertiesOpt));
            JiraQueryBuilder.AddBoolean(queryParams, "updateHistory", updateHistory);
            JiraQueryBuilder.AddBoolean(queryParams, "failFast", failFast);

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}",
                queryParams,
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
                extractSupplied ? $"fields.{extractField}" : null);
            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
        return view;
    }
}




