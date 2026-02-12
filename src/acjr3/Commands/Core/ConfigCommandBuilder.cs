using System.CommandLine;
using System.CommandLine.Invocation;
using Acjr3.Common;

namespace Acjr3.Commands.Core;

public static class ConfigCommandBuilder
{
    private static readonly string[] KnownConfigKeys =
    [
        "ACJR3_SITE_URL",
        "ACJR3_AUTH_MODE",
        "ACJR3_EMAIL",
        "ACJR3_API_TOKEN",
        "ACJR3_BEARER_TOKEN",
        "ACJR3_TIMEOUT_SECONDS",
        "ACJR3_MAX_RETRIES",
        "ACJR3_RETRY_BASE_DELAY_MS",
        "ACJR3_OPENAPI_CACHE_PATH"
    ];

    public static Command Build()
    {
        var config = new Command("config", "Configuration commands.");
        var check = new Command("check", "Validate environment configuration.");
        var show = new Command("show", "Show current acjr3 configuration values.");
        var set = new Command("set", "Set an acjr3 environment variable.");
        var init = new Command("init", "Initialize acjr3 configuration values.");

        check.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var logger = new ConsoleLogger(false);
            if (!RuntimeConfigLoader.TryLoadValidatedConfig(requireAuth: true, logger, out var settings, out var error))
            {
                var (exitCode, errorCode) = ConfigErrorClassifier.Classify(error);
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(errorCode, error, null, null), (int)exitCode);
                return;
            }

            CliEnvelopeWriter.Write(context, null, output, true, new
            {
                siteUrl = settings!.SiteUrl.ToString(),
                authMode = settings.AuthMode.ToString().ToLowerInvariant(),
                email = Redactor.MaskEmail(settings.Email),
                apiToken = Redactor.MaskSecret(settings.ApiToken),
                bearerToken = Redactor.MaskSecret(settings.BearerToken),
                timeoutSeconds = settings.TimeoutSeconds,
                maxRetries = settings.MaxRetries,
                retryBaseDelayMs = settings.RetryBaseDelayMs
            }, null, (int)CliExitCode.Success);
        });

        show.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            CliEnvelopeWriter.Write(context, null, output, true, BuildConfigSnapshot(), null, (int)CliExitCode.Success);
        });

        var keyArg = new Argument<string>("key", "Environment variable key (for example ACJR3_SITE_URL).");
        var valueArg = new Argument<string>("value", "Environment variable value.");
        var targetOpt = new Option<string>("--target", () => "user", "Environment target: process|user.");
        var yesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var forceOpt = new Option<bool>("--force", "Force mutating operations.");
        set.AddArgument(keyArg);
        set.AddArgument(valueArg);
        set.AddOption(targetOpt);
        set.AddOption(yesOpt);
        set.AddOption(forceOpt);
        set.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            if (!context.ParseResult.GetValueForOption(yesOpt) && !context.ParseResult.GetValueForOption(forceOpt))
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, "Mutating config commands require --yes or --force.", null, null), (int)CliExitCode.Validation);
                return;
            }

            var key = context.ParseResult.GetValueForArgument(keyArg);
            var value = context.ParseResult.GetValueForArgument(valueArg);
            var targetRaw = context.ParseResult.GetValueForOption(targetOpt);

            if (!TryResolveEnvironmentTarget(targetRaw, out var target, out var error))
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, error, null, null), (int)CliExitCode.Validation);
                return;
            }

            if (!KnownConfigKeys.Contains(key))
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, $"Unsupported config key '{key}'.", new { supportedKeys = KnownConfigKeys }, null), (int)CliExitCode.Validation);
                return;
            }

            Environment.SetEnvironmentVariable(key, value, target);
            CliEnvelopeWriter.Write(context, null, output, true, new { updated = key, target = targetRaw, snapshot = BuildConfigSnapshot() }, null, (int)CliExitCode.Success);
        });

        var initTargetOpt = new Option<string>("--target", () => "user", "Environment target: process|user.");
        var siteUrlOpt = new Option<string?>("--site-url", "Jira site URL.");
        var authModeOpt = new Option<string?>("--auth-mode", "Auth mode: basic|bearer.");
        var emailOpt = new Option<string?>("--email", "Email for basic auth.");
        var apiTokenOpt = new Option<string?>("--api-token", "API token for basic auth.");
        var bearerTokenOpt = new Option<string?>("--bearer-token", "Bearer token for bearer auth.");
        var timeoutSecondsOpt = new Option<int?>("--timeout-seconds", "HTTP timeout in seconds.");
        var maxRetriesOpt = new Option<int?>("--max-retries", "Maximum retry attempts.");
        var retryBaseDelayMsOpt = new Option<int?>("--retry-base-delay-ms", "Retry base delay in milliseconds.");
        var openApiCachePathOpt = new Option<string?>("--openapi-cache-path", "OpenAPI cache file path.");
        var initYesOpt = new Option<bool>("--yes", "Confirm mutating operations.");
        var initForceOpt = new Option<bool>("--force", "Force mutating operations.");

        init.AddOption(initTargetOpt);
        init.AddOption(siteUrlOpt);
        init.AddOption(authModeOpt);
        init.AddOption(emailOpt);
        init.AddOption(apiTokenOpt);
        init.AddOption(bearerTokenOpt);
        init.AddOption(timeoutSecondsOpt);
        init.AddOption(maxRetriesOpt);
        init.AddOption(retryBaseDelayMsOpt);
        init.AddOption(openApiCachePathOpt);
        init.AddOption(initYesOpt);
        init.AddOption(initForceOpt);
        init.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            if (!context.ParseResult.GetValueForOption(initYesOpt) && !context.ParseResult.GetValueForOption(initForceOpt))
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, "Mutating config commands require --yes or --force.", null, null), (int)CliExitCode.Validation);
                return;
            }

            var parseResult = context.ParseResult;
            var targetRaw = parseResult.GetValueForOption(initTargetOpt);
            if (!TryResolveEnvironmentTarget(targetRaw, out var target, out var error))
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, error, null, null), (int)CliExitCode.Validation);
                return;
            }

            if (parseResult.GetValueForOption(timeoutSecondsOpt) is { } timeout && timeout <= 0)
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, "--timeout-seconds must be greater than zero.", null, null), (int)CliExitCode.Validation);
                return;
            }

            if (parseResult.GetValueForOption(maxRetriesOpt) is { } retries && retries < 0)
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, "--max-retries must be zero or greater.", null, null), (int)CliExitCode.Validation);
                return;
            }

            if (parseResult.GetValueForOption(retryBaseDelayMsOpt) is { } delay && delay <= 0)
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, "--retry-base-delay-ms must be greater than zero.", null, null), (int)CliExitCode.Validation);
                return;
            }

            SetIfProvided("ACJR3_SITE_URL", parseResult.GetValueForOption(siteUrlOpt), target);
            SetIfProvided("ACJR3_AUTH_MODE", parseResult.GetValueForOption(authModeOpt), target);
            SetIfProvided("ACJR3_EMAIL", parseResult.GetValueForOption(emailOpt), target);
            SetIfProvided("ACJR3_API_TOKEN", parseResult.GetValueForOption(apiTokenOpt), target);
            SetIfProvided("ACJR3_BEARER_TOKEN", parseResult.GetValueForOption(bearerTokenOpt), target);
            SetIfProvided("ACJR3_TIMEOUT_SECONDS", parseResult.GetValueForOption(timeoutSecondsOpt)?.ToString(), target);
            SetIfProvided("ACJR3_MAX_RETRIES", parseResult.GetValueForOption(maxRetriesOpt)?.ToString(), target);
            SetIfProvided("ACJR3_RETRY_BASE_DELAY_MS", parseResult.GetValueForOption(retryBaseDelayMsOpt)?.ToString(), target);
            SetIfProvided("ACJR3_OPENAPI_CACHE_PATH", parseResult.GetValueForOption(openApiCachePathOpt), target);

            CliEnvelopeWriter.Write(context, null, output, true, new { target = targetRaw, snapshot = BuildConfigSnapshot() }, null, (int)CliExitCode.Success);
        });

        config.AddCommand(check);
        config.AddCommand(show);
        config.AddCommand(set);
        config.AddCommand(init);
        return config;
    }

    private static object BuildConfigSnapshot()
    {
        return new
        {
            siteUrl = Environment.GetEnvironmentVariable("ACJR3_SITE_URL") ?? "<not set>",
            authMode = Environment.GetEnvironmentVariable("ACJR3_AUTH_MODE") ?? "<not set>",
            email = Redactor.MaskEmail(Environment.GetEnvironmentVariable("ACJR3_EMAIL")),
            apiToken = Redactor.MaskSecret(Environment.GetEnvironmentVariable("ACJR3_API_TOKEN")),
            bearerToken = Redactor.MaskSecret(Environment.GetEnvironmentVariable("ACJR3_BEARER_TOKEN")),
            timeoutSeconds = Environment.GetEnvironmentVariable("ACJR3_TIMEOUT_SECONDS") ?? "<default: 100>",
            maxRetries = Environment.GetEnvironmentVariable("ACJR3_MAX_RETRIES") ?? "<default: 5>",
            retryBaseDelayMs = Environment.GetEnvironmentVariable("ACJR3_RETRY_BASE_DELAY_MS") ?? "<default: 500>",
            openApiCachePath = Environment.GetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH") ?? "<default: local app data cache>"
        };
    }

    private static bool TryResolveEnvironmentTarget(string? raw, out EnvironmentVariableTarget target, out string error)
    {
        error = string.Empty;
        target = EnvironmentVariableTarget.User;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (raw.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            target = EnvironmentVariableTarget.User;
            return true;
        }

        if (raw.Equals("process", StringComparison.OrdinalIgnoreCase))
        {
            target = EnvironmentVariableTarget.Process;
            return true;
        }

        error = "--target must be one of: process, user.";
        return false;
    }

    private static void SetIfProvided(string key, string? value, EnvironmentVariableTarget target)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Environment.SetEnvironmentVariable(key, value, target);
        }
    }
}
