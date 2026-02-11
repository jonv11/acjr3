using System.Net.Sockets;
using System.Text;
using Acjr3.App;

namespace Acjr3.Tests.Integration;

[Collection("ProgramE2e")]
public sealed class ProgramE2eTests
{
    [Fact]
    public async Task RequestCommand_RetryThenSuccess_WritesOutFile()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            StatusCode: 429,
            ReasonPhrase: "Too Many Requests",
            Headers: new Dictionary<string, string> { ["Retry-After"] = "0", ["Content-Type"] = "application/json" },
            Body: "{\"error\":\"rate\"}"));
        server.EnqueueResponse(new ReplayResponse(
            StatusCode: 200,
            ReasonPhrase: "OK",
            Headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body: "{\"ok\":true}"));
        await server.StartAsync();

        var outPath = Path.Combine(Path.GetTempPath(), $"acjr3-e2e-{Guid.NewGuid():N}.json");
        try
        {
            var args = new[]
            {
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--timeout-seconds", "5",
                "--max-retries", "1",
                "--retry-base-delay-ms", "1",
                "request", "GET", "/rest/api/3/project",
                "--raw",
                "--out", outPath
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);

            Assert.Equal(0, exitCode);
            Assert.Equal(2, server.Requests.Count);
            Assert.Equal("Bearer token", server.Requests[0].Headers["Authorization"]);
            Assert.Equal("{\"ok\":true}", await File.ReadAllTextAsync(outPath));
        }
        finally
        {
            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }
        }
    }

    [Fact]
    public async Task RequestCommand_FailOnNonSuccess_ReturnsOne()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            StatusCode: 500,
            ReasonPhrase: "Server Error",
            Headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body: "{\"error\":\"boom\"}"));
        await server.StartAsync();

        var args = new[]
        {
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--max-retries", "0",
            "request", "GET", "/rest/api/3/project",
            "--fail-on-non-success",
            "--raw"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task RequestCommand_Paginate_MakesMultipleRequests()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            StatusCode: 200,
            ReasonPhrase: "OK",
            Headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body: "{\"startAt\":0,\"maxResults\":1,\"total\":2,\"values\":[{\"id\":\"1\"}]}"));
        server.EnqueueResponse(new ReplayResponse(
            StatusCode: 200,
            ReasonPhrase: "OK",
            Headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body: "{\"startAt\":1,\"maxResults\":1,\"total\":2,\"values\":[{\"id\":\"2\"}]}"));
        await server.StartAsync();

        var args = new[]
        {
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "request", "GET", "/rest/api/3/project",
            "--paginate",
            "--raw"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, server.Requests.Count);
        Assert.Contains("startAt=0", server.Requests[0].PathAndQuery);
        Assert.Contains("startAt=1", server.Requests[1].PathAndQuery);
        Assert.Contains("\"id\":\"1\"", stdout);
        Assert.Contains("\"id\":\"2\"", stdout);
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

[CollectionDefinition("ProgramE2e", DisableParallelization = true)]
public sealed class ProgramE2eCollection;

internal sealed record ReplayRequest(string Method, string PathAndQuery, IReadOnlyDictionary<string, string> Headers, string Body);

internal sealed record ReplayResponse(int StatusCode, string ReasonPhrase, IReadOnlyDictionary<string, string> Headers, string Body);

internal sealed class LocalReplayServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
    private readonly Queue<ReplayResponse> responses = new();
    private readonly CancellationTokenSource cts = new();
    private Task? loopTask;

    public List<ReplayRequest> Requests { get; } = [];

    public string BaseUrl { get; private set; } = string.Empty;

    public void EnqueueResponse(ReplayResponse response) => responses.Enqueue(response);

    public Task StartAsync()
    {
        listener.Start();
        var endpoint = (System.Net.IPEndPoint)listener.LocalEndpoint;
        BaseUrl = $"http://127.0.0.1:{endpoint.Port}";
        loopTask = Task.Run(() => AcceptLoopAsync(cts.Token));
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return;
        }

        var requestParts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null || line.Length == 0)
            {
                break;
            }

            var idx = line.IndexOf(':');
            if (idx <= 0)
            {
                continue;
            }

            headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }

        var body = string.Empty;
        if (headers.TryGetValue("Content-Length", out var contentLengthRaw)
            && int.TryParse(contentLengthRaw, out var contentLength)
            && contentLength > 0)
        {
            var buffer = new char[contentLength];
            var read = 0;
            while (read < contentLength)
            {
                var chunk = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), cancellationToken);
                if (chunk == 0)
                {
                    break;
                }

                read += chunk;
            }

            body = new string(buffer, 0, read);
        }

        var method = requestParts.Length > 0 ? requestParts[0] : "GET";
        var pathAndQuery = requestParts.Length > 1 ? requestParts[1] : "/";
        Requests.Add(new ReplayRequest(method, pathAndQuery, headers, body));

        if (responses.Count == 0)
        {
            await WriteResponseAsync(writer, new ReplayResponse(500, "No Response Scripted", new Dictionary<string, string>(), "{\"error\":\"no-response\"}"));
            return;
        }

        await WriteResponseAsync(writer, responses.Dequeue());
    }

    private static async Task WriteResponseAsync(StreamWriter writer, ReplayResponse response)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);
        await writer.WriteLineAsync($"HTTP/1.1 {response.StatusCode} {response.ReasonPhrase}");

        foreach (var header in response.Headers)
        {
            await writer.WriteLineAsync($"{header.Key}: {header.Value}");
        }

        if (!response.Headers.ContainsKey("Content-Length"))
        {
            await writer.WriteLineAsync($"Content-Length: {bodyBytes.Length}");
        }

        if (!response.Headers.ContainsKey("Connection"))
        {
            await writer.WriteLineAsync("Connection: close");
        }

        await writer.WriteLineAsync();
        await writer.BaseStream.WriteAsync(bodyBytes);
        await writer.BaseStream.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        listener.Stop();
        if (loopTask != null)
        {
            try
            {
                await loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
    }
}
