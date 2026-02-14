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
    private static Command BuildTransitionCommand(IServiceProvider services)
    {
        var transition = new Command("transition", "Transition an issue. Use `issue transition list <key>` to list available transitions.");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var toOpt = new Option<string?>("--to", "Target transition name (for example, Done)");
        var idOpt = new Option<string?>("--id", "Target transition ID");
        var inOpt = new Option<string?>("--in", "Path to request payload file, or '-' for stdin.");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success", "Allow 4xx/5xx responses without forcing a non-zero exit.");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        transition.AddArgument(keyArg);
        transition.AddOption(toOpt);
        transition.AddOption(idOpt);
        transition.AddOption(inOpt);
        transition.AddOption(yesOpt);
        transition.AddOption(forceOpt);
        transition.AddOption(allowNonSuccessOpt);
        transition.AddOption(verboseOpt);
        transition.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            var key = parseResult.GetValueForArgument(keyArg);
            var to = parseResult.GetValueForOption(toOpt);
            var id = parseResult.GetValueForOption(idOpt);

            var transitionBasePayload = await TryResolveIssueTransitionBasePayloadAsync(
                parseResult.GetValueForOption(inOpt),
                context,
                context.GetCancellationToken());
            if (!transitionBasePayload.Ok)
            {
                return;
            }
            var payloadObject = transitionBasePayload.Payload!;

            if (!string.IsNullOrWhiteSpace(to) && !string.IsNullOrWhiteSpace(id))
            {
                CliOutput.WriteValidationError(context, "Provide either --to or --id, not both.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                JsonPayloadPipeline.SetString(payloadObject, id, "transition", "id");
            }
            else if (!string.IsNullOrWhiteSpace(to))
            {
                var transitionId = await ResolveTransitionIdByNameAsync(
                    services,
                    config!,
                    key,
                    to,
                    logger,
                    context.GetCancellationToken());
                if (string.IsNullOrWhiteSpace(transitionId))
                {
                    CliOutput.WriteValidationError(context, $"Could not resolve transition '{to}' for issue '{key}'.");
                    return;
                }

                JsonPayloadPipeline.SetString(payloadObject, transitionId, "transition", "id");
            }

            if (string.IsNullOrWhiteSpace(JsonPayloadPipeline.TryGetString(payloadObject, "transition", "id")))
            {
                CliOutput.WriteValidationError(context, "Final payload must include transition.id (set --id/--to or provide it in the base payload).");
                return;
            }

            var transitionOptions = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                $"/rest/api/3/issue/{key}/transitions",
                new List<KeyValuePair<string, string>>(),
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
            var exitCode = await executor.ExecuteAsync(config!, transitionOptions, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        transition.AddCommand(BuildTransitionListCommand(services));
        return transition;
    }

    private static Command BuildTransitionListCommand(IServiceProvider services)
    {
        var list = new Command("list", "List available transitions for an issue key (`issue transition list <key>`).");
        var keyArg = new Argument<string>("key", "Issue key (for example, TEST-123)") { Arity = ArgumentArity.ExactlyOne };
        var expandOpt = new Option<string?>("--expand", "Expand transition response entities");
        var transitionIdOpt = new Option<string?>("--transition-id", "Filter by transition ID");
        var skipRemoteOnlyConditionOpt = new Option<string?>("--skip-remote-only-condition", "Skip remote-only condition check (true|false)");
        var includeUnavailableTransitionsOpt = new Option<string?>("--include-unavailable-transitions", "Include unavailable transitions (true|false)");
        var sortByOpsBarAndStatusOpt = new Option<string?>("--sort-by-ops-bar-and-status", "Sort by ops bar and status (true|false)");
        var allowNonSuccessOpt = new Option<bool>("--allow-non-success", "Allow 4xx/5xx responses without forcing a non-zero exit.");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        list.AddArgument(keyArg);
        list.AddOption(expandOpt);
        list.AddOption(transitionIdOpt);
        list.AddOption(skipRemoteOnlyConditionOpt);
        list.AddOption(includeUnavailableTransitionsOpt);
        list.AddOption(sortByOpsBarAndStatusOpt);
        list.AddOption(allowNonSuccessOpt);
        list.AddOption(verboseOpt);

        list.SetHandler(async (InvocationContext context) =>
        {
            if (!JiraCommandPreflight.TryPrepare(context, verboseOpt, out var parseResult, out var logger, out var config, out var outputPreferences))
            {
                return;
            }

            if (!TryParseBooleanOption(parseResult.GetValueForOption(skipRemoteOnlyConditionOpt), "--skip-remote-only-condition", context, out var skipRemoteOnlyCondition)
                || !TryParseBooleanOption(parseResult.GetValueForOption(includeUnavailableTransitionsOpt), "--include-unavailable-transitions", context, out var includeUnavailableTransitions)
                || !TryParseBooleanOption(parseResult.GetValueForOption(sortByOpsBarAndStatusOpt), "--sort-by-ops-bar-and-status", context, out var sortByOpsBarAndStatus))
            {
                return;
            }

            var query = new List<KeyValuePair<string, string>>();
            JiraQueryBuilder.AddString(query, "expand", parseResult.GetValueForOption(expandOpt));
            JiraQueryBuilder.AddString(query, "transitionId", parseResult.GetValueForOption(transitionIdOpt));
            JiraQueryBuilder.AddBoolean(query, "skipRemoteOnlyCondition", skipRemoteOnlyCondition);
            JiraQueryBuilder.AddBoolean(query, "includeUnavailableTransitions", includeUnavailableTransitions);
            JiraQueryBuilder.AddBoolean(query, "sortByOpsBarAndStatus", sortByOpsBarAndStatus);

            var key = parseResult.GetValueForArgument(keyArg);
            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Get,
                $"/rest/api/3/issue/{key}/transitions",
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

    private static async Task<string?> ResolveTransitionIdByNameAsync(
        IServiceProvider services,
        Acjr3Config config,
        string issueKey,
        string transitionName,
        IAppLogger logger,
        CancellationToken cancellationToken)
    {
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("acjr3");
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

        var url = UrlBuilder.Build(config.SiteUrl, $"/rest/api/3/issue/{issueKey}/transitions", []);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        var auth = services.GetRequiredService<AuthHeaderProvider>().Create(config);
        request.Headers.Authorization = auth;

        logger.Verbose($"Resolving transition id by name '{transitionName}' for issue {issueKey}");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Failed to resolve transition name '{transitionName}' on issue {issueKey}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            var text = Encoding.UTF8.GetString(payload);
            if (!string.IsNullOrWhiteSpace(text))
            {
                Console.Error.WriteLine(text);
            }

            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("transitions", out var transitions)
                || transitions.ValueKind != JsonValueKind.Array)
            {
                Console.Error.WriteLine("Transition response does not include a transitions array.");
                return null;
            }

            string? fallbackId = null;
            var availableNames = new List<string>();
            foreach (var item in transitions.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (!item.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var name = nameElement.GetString()!;
                availableNames.Add(name);
                if (name.Equals(transitionName, StringComparison.OrdinalIgnoreCase))
                {
                    fallbackId = idElement.GetString();
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackId))
            {
                return fallbackId;
            }

            Console.Error.WriteLine($"Transition name '{transitionName}' not found for issue {issueKey}.");
            if (availableNames.Count > 0)
            {
                Console.Error.WriteLine($"Available transitions: {string.Join(", ", availableNames)}");
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse transitions response: {ex.Message}");
            return null;
        }
    }
}

