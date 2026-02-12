using Acjr3.App;

namespace Acjr3.Tests.Integration;

public sealed partial class ProgramE2eTests
{
    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"acjr3-e2e-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return path;
    }

    private static readonly SemaphoreSlim ProgramSemaphore = new(1, 1);
    private static readonly string[] ManagedEnvKeys =
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

    private static async Task<(int ExitCode, string Stdout, string Stderr)> InvokeProgramAsync(string[] args)
    {
        await ProgramSemaphore.WaitAsync();
        var envSnapshot = ManagedEnvKeys.ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            var exitCode = await Program.Main(args);
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            foreach (var item in envSnapshot)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value, EnvironmentVariableTarget.Process);
            }

            ProgramSemaphore.Release();
        }
    }
}
