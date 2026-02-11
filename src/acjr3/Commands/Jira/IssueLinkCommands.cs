using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static class IssueLinkCommands
{
    public static Command BuildIssueLinkCommand(IServiceProvider services)
    {
        var issueLink = new Command("issuelink", "Create Jira issue links");
        var typeOpt = new Option<string?>("--type", "Issue link type name (for example Blocks)");
        var inwardOpt = new Option<string?>("--inward", "Inward issue key (for example ACJ-123)");
        var outwardOpt = new Option<string?>("--outward", "Outward issue key (for example ACJ-456)");
        var bodyOpt = new Option<string?>("--body", "Inline JSON payload matching Jira issue-link schema");
        var bodyFileOpt = new Option<string?>("--body-file", "Read JSON payload from file path");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");

        issueLink.AddOption(typeOpt);
        issueLink.AddOption(inwardOpt);
        issueLink.AddOption(outwardOpt);
        issueLink.AddOption(bodyOpt);
        issueLink.AddOption(bodyFileOpt);
        issueLink.AddOption(rawOpt);
        issueLink.AddOption(failOnNonSuccessOpt);
        issueLink.AddOption(verboseOpt);

        issueLink.SetHandler(async (InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));
            if (!Program.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            if (!TryResolveBody(
                    parseResult.GetValueForOption(bodyOpt),
                    parseResult.GetValueForOption(bodyFileOpt),
                    context,
                    out var resolvedBody))
            {
                return;
            }

            if (resolvedBody is null)
            {
                var type = parseResult.GetValueForOption(typeOpt);
                var inward = parseResult.GetValueForOption(inwardOpt);
                var outward = parseResult.GetValueForOption(outwardOpt);
                if (string.IsNullOrWhiteSpace(type)
                    || string.IsNullOrWhiteSpace(inward)
                    || string.IsNullOrWhiteSpace(outward))
                {
                    Console.Error.WriteLine("Either provide --body/--body-file, or provide --type, --inward, and --outward.");
                    context.ExitCode = 1;
                    return;
                }

                var payload = new Dictionary<string, object?>
                {
                    ["type"] = new Dictionary<string, string> { ["name"] = type },
                    ["inwardIssue"] = new Dictionary<string, string> { ["key"] = inward },
                    ["outwardIssue"] = new Dictionary<string, string> { ["key"] = outward }
                };
                resolvedBody = JsonSerializer.Serialize(payload);
            }

            var options = new RequestCommandOptions(
                System.Net.Http.HttpMethod.Post,
                "/rest/api/3/issueLink",
                new List<KeyValuePair<string, string>>(),
                new List<KeyValuePair<string, string>>(),
                "application/json",
                "application/json",
                resolvedBody,
                null,
                parseResult.GetValueForOption(rawOpt),
                false,
                (parseResult.FindResultFor(failOnNonSuccessOpt) is null || parseResult.GetValueForOption(failOnNonSuccessOpt)),
                false,
                false,
                false);

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return issueLink;
    }

    private static bool TryResolveBody(string? body, string? bodyFile, InvocationContext context, out string? resolvedBody)
    {
        resolvedBody = null;

        if (!string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(bodyFile))
        {
            Console.Error.WriteLine("Use either --body or --body-file, not both.");
            context.ExitCode = 1;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(bodyFile))
        {
            try
            {
                resolvedBody = File.ReadAllText(bodyFile!);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read body file '{bodyFile}': {ex.Message}");
                context.ExitCode = 1;
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(body))
        {
            resolvedBody = body;
        }

        return true;
    }
}
