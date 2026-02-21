using System.CommandLine;

namespace Acjr3.App;

public static class RuntimeOverrideOptions
{
    private static readonly IReadOnlyDictionary<string, string> RuntimeOverrideMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--site-url"] = "ACJR3_SITE_URL",
        ["--auth-mode"] = "ACJR3_AUTH_MODE",
        ["--email"] = "ACJR3_EMAIL",
        ["--api-token"] = "ACJR3_API_TOKEN",
        ["--bearer-token"] = "ACJR3_BEARER_TOKEN",
        ["--timeout-seconds"] = "ACJR3_TIMEOUT_SECONDS",
        ["--max-retries"] = "ACJR3_MAX_RETRIES",
        ["--retry-base-delay-ms"] = "ACJR3_RETRY_BASE_DELAY_MS",
        ["--openapi-cache-path"] = "ACJR3_OPENAPI_CACHE_PATH"
    };

    public static void AddRuntimeOverrideOptions(Command command)
    {
        command.AddGlobalOption(new Option<string?>("--site-url") { Description = "Override ACJR3_SITE_URL for this invocation." });
        command.AddGlobalOption(new Option<string?>("--auth-mode") { Description = "Override ACJR3_AUTH_MODE (basic|bearer) for this invocation." });
        command.AddGlobalOption(new Option<string?>("--email") { Description = "Override ACJR3_EMAIL for this invocation." });
        command.AddGlobalOption(new Option<string?>("--api-token") { Description = "Override ACJR3_API_TOKEN for this invocation." });
        command.AddGlobalOption(new Option<string?>("--bearer-token") { Description = "Override ACJR3_BEARER_TOKEN for this invocation." });
        command.AddGlobalOption(new Option<int?>("--timeout-seconds") { Description = "Override ACJR3_TIMEOUT_SECONDS for this invocation." });
        command.AddGlobalOption(new Option<int?>("--max-retries") { Description = "Override ACJR3_MAX_RETRIES for this invocation." });
        command.AddGlobalOption(new Option<int?>("--retry-base-delay-ms") { Description = "Override ACJR3_RETRY_BASE_DELAY_MS for this invocation." });
        command.AddGlobalOption(new Option<string?>("--openapi-cache-path") { Description = "Override ACJR3_OPENAPI_CACHE_PATH for this invocation." });
    }

    public static void ApplyProcessConfigOverrides(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (!RuntimeOverrideMap.TryGetValue(current, out var envName))
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

            Environment.SetEnvironmentVariable(envName, value, EnvironmentVariableTarget.Process);
        }
    }
}

