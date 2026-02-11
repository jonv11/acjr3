using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acjr3.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ApplyProcessConfigOverrides(args);

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter("Acjr3", LogLevel.Debug);
        });
        services.AddHttpClient("acjr3");
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<RetryPolicy>();
        services.AddSingleton<AuthHeaderProvider>();
        services.AddSingleton<ResponseFormatter>();
        services.AddSingleton<RequestExecutor>();
        services.AddSingleton<OpenApiService>();
        var provider = services.BuildServiceProvider();
        ConsoleLogger.SetLoggerFactory(provider.GetRequiredService<ILoggerFactory>());

        var rootCommand = BuildRootCommand(provider);
        return await rootCommand.InvokeAsync(args);
    }

    private static RootCommand BuildRootCommand(IServiceProvider services)
    {
        var root = new RootCommand("acjr3 - Atlassian Cloud Jira REST API v3 proxy CLI");
        root.AddGlobalOption(new Option<string?>("--site-url", "Override ACJR3_SITE_URL for this invocation"));
        root.AddGlobalOption(new Option<string?>("--auth-mode", "Override ACJR3_AUTH_MODE (basic|bearer) for this invocation"));
        root.AddGlobalOption(new Option<string?>("--email", "Override ACJR3_EMAIL for this invocation"));
        root.AddGlobalOption(new Option<string?>("--api-token", "Override ACJR3_API_TOKEN for this invocation"));
        root.AddGlobalOption(new Option<string?>("--bearer-token", "Override ACJR3_BEARER_TOKEN for this invocation"));
        root.AddGlobalOption(new Option<int?>("--timeout-seconds", "Override ACJR3_TIMEOUT_SECONDS for this invocation"));
        root.AddGlobalOption(new Option<int?>("--max-retries", "Override ACJR3_MAX_RETRIES for this invocation"));
        root.AddGlobalOption(new Option<int?>("--retry-base-delay-ms", "Override ACJR3_RETRY_BASE_DELAY_MS for this invocation"));
        root.AddGlobalOption(new Option<string?>("--openapi-cache-path", "Override ACJR3_OPENAPI_CACHE_PATH for this invocation"));
        root.AddCommand(BuildRequestCommand(services));
        root.AddCommand(BuildConfigCommand());
        root.AddCommand(BuildOpenApiCommand(services));
        root.AddCommand(IssueCommands.BuildIssueCommand(services));
        root.AddCommand(PriorityCommands.BuildPriorityCommand(services));
        root.AddCommand(SearchCommands.BuildSearchCommand(services));
        root.AddCommand(StatusCommands.BuildStatusCommand(services));
        root.AddCommand(ProjectCommands.BuildProjectCommand(services));
        root.AddCommand(IssueTypeCommands.BuildIssueTypeCommand(services));
        root.AddCommand(UserCommands.BuildUserCommand(services));
        root.AddCommand(FieldCommands.BuildFieldCommand(services));
        root.AddCommand(GroupCommands.BuildGroupCommand(services));
        root.AddCommand(RoleCommands.BuildRoleCommand(services));
        root.AddCommand(ResolutionCommands.BuildResolutionCommand(services));
        return root;
    }

    private static void ApplyProcessConfigOverrides(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                continue;
            }

            var value = args[i + 1];
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            switch (current)
            {
                case "--site-url":
                    Environment.SetEnvironmentVariable("ACJR3_SITE_URL", value, EnvironmentVariableTarget.Process);
                    break;
                case "--auth-mode":
                    Environment.SetEnvironmentVariable("ACJR3_AUTH_MODE", value, EnvironmentVariableTarget.Process);
                    break;
                case "--email":
                    Environment.SetEnvironmentVariable("ACJR3_EMAIL", value, EnvironmentVariableTarget.Process);
                    break;
                case "--api-token":
                    Environment.SetEnvironmentVariable("ACJR3_API_TOKEN", value, EnvironmentVariableTarget.Process);
                    break;
                case "--bearer-token":
                    Environment.SetEnvironmentVariable("ACJR3_BEARER_TOKEN", value, EnvironmentVariableTarget.Process);
                    break;
                case "--timeout-seconds":
                    Environment.SetEnvironmentVariable("ACJR3_TIMEOUT_SECONDS", value, EnvironmentVariableTarget.Process);
                    break;
                case "--max-retries":
                    Environment.SetEnvironmentVariable("ACJR3_MAX_RETRIES", value, EnvironmentVariableTarget.Process);
                    break;
                case "--retry-base-delay-ms":
                    Environment.SetEnvironmentVariable("ACJR3_RETRY_BASE_DELAY_MS", value, EnvironmentVariableTarget.Process);
                    break;
                case "--openapi-cache-path":
                    Environment.SetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH", value, EnvironmentVariableTarget.Process);
                    break;
            }
        }
    }

    private static Command BuildRequestCommand(IServiceProvider services)
    {
        var command = new Command("request", "Universal Jira REST API proxy");

        var methodArg = new Argument<string>("method", "HTTP method: GET|POST|PUT|DELETE|PATCH");
        var pathArg = new Argument<string>("path", "Relative Jira path, e.g. /rest/api/3/myself");

        var queryOpt = new Option<string[]>("--query", "Query parameter key=value")
        {
            AllowMultipleArgumentsPerToken = true,
        };
        var headerOpt = new Option<string[]>("--header", "Header key=value")
        {
            AllowMultipleArgumentsPerToken = true,
        };
        var acceptOpt = new Option<string>("--accept", () => "application/json", "Accept header value");
        var contentTypeOpt = new Option<string?>("--content-type", "Content-Type header value");
        var bodyOpt = new Option<string?>("--body", "Inline request body");
        var bodyFileOpt = new Option<string?>("--body-file", "Read request body from file path");
        var outOpt = new Option<string?>("--out", "Write response body to file path");
        var rawOpt = new Option<bool>("--raw", "Do not pretty-print JSON response");
        var includeHeadersOpt = new Option<bool>("--include-headers", "Include response headers in output");
        var failOnNonSuccessOpt = new Option<bool>("--fail-on-non-success", "Exit non-zero on 4xx/5xx responses");
        var dryRunOpt = new Option<bool>("--dry-run", "Print request summary without sending");
        var verboseOpt = new Option<bool>("--verbose", "Enable verbose diagnostics logging");
        var retryNonIdempotentOpt = new Option<bool>("--retry-non-idempotent", "Allow retries for POST/PATCH methods");
        var paginateOpt = new Option<bool>("--paginate", "Best-effort pagination for GET endpoints");

        command.AddArgument(methodArg);
        command.AddArgument(pathArg);

        command.AddOption(queryOpt);
        command.AddOption(headerOpt);
        command.AddOption(acceptOpt);
        command.AddOption(contentTypeOpt);
        command.AddOption(bodyOpt);
        command.AddOption(bodyFileOpt);
        command.AddOption(outOpt);
        command.AddOption(rawOpt);
        command.AddOption(includeHeadersOpt);
        command.AddOption(failOnNonSuccessOpt);
        command.AddOption(dryRunOpt);
        command.AddOption(verboseOpt);
        command.AddOption(retryNonIdempotentOpt);
        command.AddOption(paginateOpt);

        command.SetHandler(async context =>
        {
            var parseResult = context.ParseResult;
            var logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOpt));

            if (!TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
            {
                Console.Error.WriteLine(configError);
                context.ExitCode = 1;
                return;
            }

            var method = parseResult.GetValueForArgument(methodArg);
            if (!HttpMethodParser.TryParse(method, out var httpMethod, out var methodError))
            {
                Console.Error.WriteLine(methodError);
                context.ExitCode = 1;
                return;
            }

            var path = parseResult.GetValueForArgument(pathArg);
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.Error.WriteLine("PATH is required.");
                context.ExitCode = 1;
                return;
            }

            var body = parseResult.GetValueForOption(bodyOpt);
            var bodyFile = parseResult.GetValueForOption(bodyFileOpt);
            if (!string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(bodyFile))
            {
                Console.Error.WriteLine("Use either --body or --body-file, not both.");
                context.ExitCode = 1;
                return;
            }

            if (!RequestOptionParser.TryParsePairs(parseResult.GetValueForOption(queryOpt) ?? [], out var queryPairs, out var queryError))
            {
                Console.Error.WriteLine(queryError);
                context.ExitCode = 1;
                return;
            }

            if (!RequestOptionParser.TryParsePairs(parseResult.GetValueForOption(headerOpt) ?? [], out var headerPairs, out var headerError))
            {
                Console.Error.WriteLine(headerError);
                context.ExitCode = 1;
                return;
            }

            string? bodyValue = body;
            if (!string.IsNullOrWhiteSpace(bodyFile))
            {
                try
                {
                    bodyValue = await File.ReadAllTextAsync(bodyFile!, context.GetCancellationToken());
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to read body file '{bodyFile}': {ex.Message}");
                    context.ExitCode = 1;
                    return;
                }
            }

            var options = new RequestCommandOptions(
                httpMethod!,
                path,
                queryPairs,
                headerPairs,
                parseResult.GetValueForOption(acceptOpt)!,
                parseResult.GetValueForOption(contentTypeOpt),
                bodyValue,
                parseResult.GetValueForOption(outOpt),
                parseResult.GetValueForOption(rawOpt),
                parseResult.GetValueForOption(includeHeadersOpt),
                parseResult.GetValueForOption(failOnNonSuccessOpt),
                parseResult.GetValueForOption(dryRunOpt),
                parseResult.GetValueForOption(retryNonIdempotentOpt),
                parseResult.GetValueForOption(paginateOpt));

            var executor = services.GetRequiredService<RequestExecutor>();
            var exitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });

        return command;
    }

    private static Command BuildConfigCommand()
    {
        var config = new Command("config", "Configuration commands");
        var check = new Command("check", "Validate environment configuration");
        var show = new Command("show", "Show current acjr3 configuration values");
        var set = new Command("set", "Set an acjr3 environment variable");
        var init = new Command("init", "Initialize acjr3 configuration values");

        check.SetHandler((InvocationContext context) =>
        {
            var logger = new ConsoleLogger(false);
            if (!TryLoadValidatedConfig(requireAuth: true, logger, out var settings, out var error))
            {
                Console.Error.WriteLine(error);
                context.ExitCode = 1;
                return;
            }

            Console.WriteLine("Configuration OK");
            Console.WriteLine($"Base URL: {settings!.SiteUrl}");
            Console.WriteLine($"Auth mode: {settings.AuthMode}");
            Console.WriteLine($"Email: {Redactor.MaskEmail(settings.Email)}");
            Console.WriteLine($"API token: {Redactor.MaskSecret(settings.ApiToken)}");
            Console.WriteLine($"Bearer token: {Redactor.MaskSecret(settings.BearerToken)}");
            Console.WriteLine($"Timeout seconds: {settings.TimeoutSeconds}");
            Console.WriteLine($"Max retries: {settings.MaxRetries}");
            Console.WriteLine($"Retry base delay ms: {settings.RetryBaseDelayMs}");
            context.ExitCode = 0;
        });

        show.SetHandler((InvocationContext context) =>
        {
            PrintConfigSnapshot();
            context.ExitCode = 0;
        });

        var keyArg = new Argument<string>("key", "Environment variable key (for example ACJR3_SITE_URL)");
        var valueArg = new Argument<string>("value", "Environment variable value");
        var targetOpt = new Option<string>("--target", () => "user", "Environment target: process|user");
        set.AddArgument(keyArg);
        set.AddArgument(valueArg);
        set.AddOption(targetOpt);
        set.SetHandler((InvocationContext context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArg);
            var value = context.ParseResult.GetValueForArgument(valueArg);
            var targetRaw = context.ParseResult.GetValueForOption(targetOpt);

            if (!TryResolveEnvironmentTarget(targetRaw, out var target, out var error))
            {
                Console.Error.WriteLine(error);
                context.ExitCode = 1;
                return;
            }

            if (!KnownConfigKeys.Contains(key))
            {
                Console.Error.WriteLine($"Unsupported config key '{key}'.");
                Console.Error.WriteLine($"Supported keys: {string.Join(", ", KnownConfigKeys)}");
                context.ExitCode = 1;
                return;
            }

            Environment.SetEnvironmentVariable(key, value, target);
            Console.WriteLine($"Set {key} at target '{targetRaw}'.");
            PrintConfigSnapshot();
            context.ExitCode = 0;
        });

        var initTargetOpt = new Option<string>("--target", () => "user", "Environment target: process|user");
        var siteUrlOpt = new Option<string?>("--site-url", "Jira site URL");
        var authModeOpt = new Option<string?>("--auth-mode", "Auth mode: basic|bearer");
        var emailOpt = new Option<string?>("--email", "Email for basic auth");
        var apiTokenOpt = new Option<string?>("--api-token", "API token for basic auth");
        var bearerTokenOpt = new Option<string?>("--bearer-token", "Bearer token for bearer auth");
        var timeoutSecondsOpt = new Option<int?>("--timeout-seconds", "HTTP timeout in seconds");
        var maxRetriesOpt = new Option<int?>("--max-retries", "Maximum retry attempts");
        var retryBaseDelayMsOpt = new Option<int?>("--retry-base-delay-ms", "Retry base delay in milliseconds");
        var openApiCachePathOpt = new Option<string?>("--openapi-cache-path", "OpenAPI cache file path");

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
        init.SetHandler((InvocationContext context) =>
        {
            var parseResult = context.ParseResult;
            var targetRaw = parseResult.GetValueForOption(initTargetOpt);
            if (!TryResolveEnvironmentTarget(targetRaw, out var target, out var error))
            {
                Console.Error.WriteLine(error);
                context.ExitCode = 1;
                return;
            }

            if (parseResult.GetValueForOption(timeoutSecondsOpt) is { } timeout && timeout <= 0)
            {
                Console.Error.WriteLine("--timeout-seconds must be greater than zero.");
                context.ExitCode = 1;
                return;
            }

            if (parseResult.GetValueForOption(maxRetriesOpt) is { } retries && retries < 0)
            {
                Console.Error.WriteLine("--max-retries must be zero or greater.");
                context.ExitCode = 1;
                return;
            }

            if (parseResult.GetValueForOption(retryBaseDelayMsOpt) is { } delay && delay <= 0)
            {
                Console.Error.WriteLine("--retry-base-delay-ms must be greater than zero.");
                context.ExitCode = 1;
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

            Console.WriteLine($"Config initialization applied to target '{targetRaw}'.");
            PrintConfigSnapshot();
            context.ExitCode = 0;
        });

        config.AddCommand(check);
        config.AddCommand(show);
        config.AddCommand(set);
        config.AddCommand(init);
        return config;
    }

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

    private static void PrintConfigSnapshot()
    {
        Console.WriteLine("Current configuration snapshot:");
        Console.WriteLine($"ACJR3_SITE_URL={Environment.GetEnvironmentVariable("ACJR3_SITE_URL") ?? "<not set>"}");
        Console.WriteLine($"ACJR3_AUTH_MODE={Environment.GetEnvironmentVariable("ACJR3_AUTH_MODE") ?? "<not set>"}");
        Console.WriteLine($"ACJR3_EMAIL={Redactor.MaskEmail(Environment.GetEnvironmentVariable("ACJR3_EMAIL"))}");
        Console.WriteLine($"ACJR3_API_TOKEN={Redactor.MaskSecret(Environment.GetEnvironmentVariable("ACJR3_API_TOKEN"))}");
        Console.WriteLine($"ACJR3_BEARER_TOKEN={Redactor.MaskSecret(Environment.GetEnvironmentVariable("ACJR3_BEARER_TOKEN"))}");
        Console.WriteLine($"ACJR3_TIMEOUT_SECONDS={Environment.GetEnvironmentVariable("ACJR3_TIMEOUT_SECONDS") ?? "<default: 100>"}");
        Console.WriteLine($"ACJR3_MAX_RETRIES={Environment.GetEnvironmentVariable("ACJR3_MAX_RETRIES") ?? "<default: 5>"}");
        Console.WriteLine($"ACJR3_RETRY_BASE_DELAY_MS={Environment.GetEnvironmentVariable("ACJR3_RETRY_BASE_DELAY_MS") ?? "<default: 500>"}");
        Console.WriteLine($"ACJR3_OPENAPI_CACHE_PATH={Environment.GetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH") ?? "<default: local app data cache>"}");
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

    private static Command BuildOpenApiCommand(IServiceProvider services)
    {
        var openapi = new Command("openapi", "OpenAPI discovery commands");

        var fetch = new Command("fetch", "Fetch Jira Cloud REST OpenAPI spec to cache or output file");
        var fetchOut = new Option<string?>("--out", "Output file path. Defaults to local cache path.");
        var fetchSpecUrl = new Option<string?>("--spec-url", "Optional explicit OpenAPI URL.");
        fetch.AddOption(fetchOut);
        fetch.AddOption(fetchSpecUrl);
        fetch.SetHandler(async (InvocationContext context) =>
        {
            var outPath = context.ParseResult.GetValueForOption(fetchOut);
            var specUrl = context.ParseResult.GetValueForOption(fetchSpecUrl);
            var service = services.GetRequiredService<OpenApiService>();
            var logger = new ConsoleLogger(false);
            var result = await service.FetchAsync(outPath, specUrl, logger);
            if (!result.Success)
            {
                Console.Error.WriteLine(result.Message);
                context.ExitCode = 1;
                return;
            }

            Console.WriteLine(result.Message);
            context.ExitCode = 0;
        });

        var paths = new Command("paths", "List paths and methods from cached or provided OpenAPI spec");
        var filterOpt = new Option<string?>("--filter", "Filter text for path/method/operationId");
        var specFileOpt = new Option<string?>("--spec-file", "Use local OpenAPI spec file instead of cache");
        paths.AddOption(filterOpt);
        paths.AddOption(specFileOpt);
        paths.SetHandler((InvocationContext context) =>
        {
            var filter = context.ParseResult.GetValueForOption(filterOpt);
            var specFile = context.ParseResult.GetValueForOption(specFileOpt);
            var service = services.GetRequiredService<OpenApiService>();
            var result = service.ListPaths(filter, specFile);
            if (!result.Success)
            {
                Console.Error.WriteLine(result.Message);
                context.ExitCode = 1;
                return;
            }

            foreach (var line in result.Lines)
            {
                Console.WriteLine(line);
            }

            context.ExitCode = 0;
        });

        var show = new Command("show", "Show method/path details from OpenAPI spec");
        var methodArg = new Argument<string>("method", "HTTP method");
        var pathArg = new Argument<string>("path", "OpenAPI path");
        var showSpecFileOpt = new Option<string?>("--spec-file", "Use local OpenAPI spec file instead of cache");
        show.AddArgument(methodArg);
        show.AddArgument(pathArg);
        show.AddOption(showSpecFileOpt);
        show.SetHandler((InvocationContext context) =>
        {
            var method = context.ParseResult.GetValueForArgument(methodArg);
            var path = context.ParseResult.GetValueForArgument(pathArg);
            var specFile = context.ParseResult.GetValueForOption(showSpecFileOpt);
            var service = services.GetRequiredService<OpenApiService>();
            var result = service.ShowOperation(method, path, specFile);
            if (!result.Success)
            {
                Console.Error.WriteLine(result.Message);
                context.ExitCode = 1;
                return;
            }

            foreach (var line in result.Lines)
            {
                Console.WriteLine(line);
            }

            context.ExitCode = 0;
        });

        openapi.AddCommand(fetch);
        openapi.AddCommand(paths);
        openapi.AddCommand(show);

        return openapi;
    }

    internal static bool TryLoadValidatedConfig(bool requireAuth, IAppLogger logger, out Acjr3Config? config, out string error)
    {
        var source = new EnvironmentVariableSource();
        var loader = new ConfigLoader(source);
        if (!loader.TryLoad(out config, out error))
        {
            return false;
        }

        if (requireAuth && !ConfigValidator.TryValidateAuth(config!, out error))
        {
            return false;
        }

        logger.Verbose($"Loaded config site={config!.SiteUrl} auth={config.AuthMode}");
        return true;
    }
}


