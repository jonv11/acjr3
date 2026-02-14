using System.CommandLine;
using System.CommandLine.Invocation;
using Acjr3.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Core;

public static class AgentCommandBuilder
{
    public static Command BuildCapabilitiesCommand()
    {
        var capabilities = new Command("capabilities", "List supported CLI capabilities.");
        capabilities.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var data = new
            {
                schemaVersion = "1.0",
                outputFormats = new[] { "json", "jsonl", "text" },
                jsonStyles = new[] { "pretty", "compact" },
                inputFormats = new[] { "json", "adf" },
                exitCodes = new Dictionary<int, string>
                {
                    [0] = "Success",
                    [1] = "Validation / bad arguments",
                    [2] = "Authentication / authorization",
                    [3] = "Not found",
                    [4] = "Conflict / business rule",
                    [5] = "Network / timeout",
                    [10] = "Internal / tool-specific"
                }
            };

            CliEnvelopeWriter.Write(context, null, output, true, data, null, (int)CliExitCode.Success);
        });
        return capabilities;
    }

    public static Command BuildSchemaCommand()
    {
        var schema = new Command("schema", "Show machine-readable schema summary for a command.");
        var commandArg = new Argument<string?>("command", "Command path.") { Arity = ArgumentArity.ZeroOrOne };
        schema.AddArgument(commandArg);
        schema.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var target = context.ParseResult.GetValueForArgument(commandArg) ?? "<root>";
            var data = new
            {
                command = target,
                envelope = "success,data,error,meta",
                format = "json|jsonl|text"
            };
            CliEnvelopeWriter.Write(context, null, output, true, data, null, (int)CliExitCode.Success);
        });
        return schema;
    }

    public static Command BuildDoctorCommand(IServiceProvider services)
    {
        var doctor = new Command("doctor", "Run environment, auth, and cache diagnostics.");
        var checkNetworkOpt = new Option<bool>("--check-network", "Perform a lightweight network check.");
        doctor.AddOption(checkNetworkOpt);
        doctor.SetHandler(async (InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var checks = new List<object>();
            var logger = new ConsoleLogger(false);

            if (!RuntimeConfigLoader.TryLoadValidatedConfig(requireAuth: false, logger, out var config, out var loadError))
            {
                checks.Add(new { name = "config-load", ok = false, detail = loadError });
                CliEnvelopeWriter.Write(context, null, output, false, new { checks }, new CliError(CliErrorCode.Validation, "Doctor checks failed.", checks, null), (int)CliExitCode.Validation);
                return;
            }

            checks.Add(new { name = "config-load", ok = true, detail = "Configuration loaded." });
            var authOk = ConfigValidator.TryValidateAuth(config!, out var authError);
            checks.Add(new { name = "auth-config", ok = authOk, detail = authOk ? "Auth configuration valid." : authError });

            if (context.ParseResult.GetValueForOption(checkNetworkOpt))
            {
                try
                {
                    var clientFactory = services.GetRequiredService<IHttpClientFactory>();
                    var client = clientFactory.CreateClient("acjr3");
                    client.Timeout = TimeSpan.FromSeconds(5);
                    using var response = await client.GetAsync(config!.SiteUrl, context.GetCancellationToken());
                    checks.Add(new { name = "network", ok = true, detail = $"HTTP {(int)response.StatusCode}" });
                }
                catch (Exception ex)
                {
                    checks.Add(new { name = "network", ok = false, detail = ex.Message });
                }
            }

            var hasFailure = checks.Any(item =>
            {
                var prop = item.GetType().GetProperty("ok");
                return prop is not null && !(bool)prop.GetValue(item)!;
            });

            if (hasFailure)
            {
                CliEnvelopeWriter.Write(context, null, output, false, new { checks }, new CliError(CliErrorCode.Validation, "Doctor checks failed.", checks, null), (int)CliExitCode.Validation);
                return;
            }

            CliEnvelopeWriter.Write(context, null, output, true, new { checks }, null, (int)CliExitCode.Success);
        });
        return doctor;
    }

    public static Command BuildAuthCommand()
    {
        var auth = new Command("auth", "Authentication helpers.");
        var status = new Command("status", "Show auth status from current configuration.");
        status.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var logger = new ConsoleLogger(false);
            if (!RuntimeConfigLoader.TryLoadValidatedConfig(requireAuth: false, logger, out var config, out var loadError))
            {
                var (exitCode, errorCode) = ConfigErrorClassifier.Classify(loadError);
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(errorCode, loadError, null, null), (int)exitCode);
                return;
            }

            if (!ConfigValidator.TryValidateAuth(config!, out var authError))
            {
                var (exitCode, errorCode) = ConfigErrorClassifier.Classify(authError);
                CliEnvelopeWriter.Write(context, null, output, false, new { mode = config!.AuthMode.ToString().ToLowerInvariant() }, new CliError(errorCode, authError, null, null), (int)exitCode);
                return;
            }

            CliEnvelopeWriter.Write(context, null, output, true, new
            {
                mode = config!.AuthMode.ToString().ToLowerInvariant(),
                configured = true,
                siteUrl = config.SiteUrl.ToString()
            }, null, (int)CliExitCode.Success);
        });
        auth.AddCommand(status);
        return auth;
    }
}
