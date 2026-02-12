using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Acjr3.App;

namespace Acjr3.Tests.Integration;

[Collection("ProgramE2e")]
public sealed class ProgramE2eTests
{
    [Fact]
    public async Task RequestCommand_Success_EmitsEnvelope()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var args = new[]
        {
            "request", "GET", "/rest/api/3/project",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"ok\": true", stdout);
    }

    [Fact]
    public async Task RequestCommand_PostWithoutYes_FailsValidation()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"name\":\"x\"}");
        try
        {
            var args = new[]
            {
                "request", "POST", "/rest/api/3/project",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--in", bodyFile
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("requires --yes or --force", stdout);
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task RequestCommand_PostWithoutExplicitBody_UsesDefaultJsonObject()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var args = new[]
        {
            "request", "POST", "/rest/api/3/project",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--yes"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.NotNull(server.LastRequest);
        using var body = JsonDocument.Parse(server.LastRequest!.Body);
        Assert.Equal(JsonValueKind.Object, body.RootElement.ValueKind);
        Assert.Empty(body.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task RequestCommand_RejectsMultipleExplicitPayloadSources()
    {
        var inFile = WriteTempFile("{\"k\":1}");
        try
        {
            var args = new[]
            {
                "request", "POST", "/rest/api/3/project",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--body", "{\"a\":1}",
                "--in", inFile,
                "--yes"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("Use exactly one explicit payload source", stdout);
        }
        finally
        {
            File.Delete(inFile);
        }
    }

    [Fact]
    public async Task IssueCreate_WithoutYes_FailsValidation()
    {
        var args = new[]
        {
            "issue", "create", "ACJ", "--summary", "Needs confirmation",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("requires --yes or --force", stdout);
    }

    [Fact]
    public async Task IssueCreate_MissingRequiredFields_FailsValidation()
    {
        var args = new[]
        {
            "issue", "create",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("fields.project.key", stdout);
    }

    [Fact]
    public async Task IssueUpdate_WithoutChanges_FailsValidation()
    {
        var args = new[]
        {
            "issue", "update", "ACJ-1",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("at least one issue update operation", stdout);
    }

    [Fact]
    public async Task IssueTransition_WithoutTransitionId_FailsValidation()
    {
        var args = new[]
        {
            "issue", "transition", "ACJ-1",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("transition.id", stdout);
    }

    [Fact]
    public async Task IssueCommentAdd_WithoutBody_FailsValidation()
    {
        var args = new[]
        {
            "issue", "comment", "add", "ACJ-1",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("non-empty body", stdout);
    }

    [Fact]
    public async Task IssueLink_WithoutRequiredFields_FailsValidation()
    {
        var args = new[]
        {
            "issuelink",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("type.name", stdout);
    }

    [Fact]
    public async Task JiraShortcut_MissingAuth_MapsToAuthenticationExitCode()
    {
        var args = new[]
        {
            "issue", "view", "ACJ-1",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", string.Empty
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal((int)CliExitCode.Authentication, exitCode);
        Assert.Contains("authentication_error", stdout);
    }

    [Fact]
    public async Task IssueCreate_BodyFileBase_AllowsSummarySugarOverride()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"fields\":{\"project\":{\"key\":\"ACJ\"},\"summary\":\"from-body\",\"issuetype\":{\"name\":\"Task\"}}}");
        try
        {
            var args = new[]
            {
                "issue", "create",
                "--body-file", bodyFile,
                "--summary", "from-sugar",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("from-sugar", body.RootElement.GetProperty("fields").GetProperty("summary").GetString());
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task IssueCommentAdd_WithInTextAndYes_Succeeds()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("Comment from file");
        try
        {
            var args = new[]
            {
                "issue", "comment", "add", "ACJ-123",
                "--in", bodyFile,
                "--input-format", "text",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"id\": \"10001\"", stdout);
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task IssueTransition_BodyBase_AllowsIdSugarOverride()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            204,
            "No Content",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            string.Empty));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"transition\":{\"id\":\"11\"}}");
        try
        {
            var args = new[]
            {
                "issue", "transition", "ACJ-123",
                "--body-file", bodyFile,
                "--id", "31",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("31", body.RootElement.GetProperty("transition").GetProperty("id").GetString());
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task IssueLink_BodyBase_AllowsSugarOverrides()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"type\":{\"name\":\"Blocks\"},\"inwardIssue\":{\"key\":\"ACJ-1\"},\"outwardIssue\":{\"key\":\"ACJ-2\"}}");
        try
        {
            var args = new[]
            {
                "issuelink",
                "--body-file", bodyFile,
                "--outward", "ACJ-99",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("ACJ-99", body.RootElement.GetProperty("outwardIssue").GetProperty("key").GetString());
            Assert.Equal("ACJ-1", body.RootElement.GetProperty("inwardIssue").GetProperty("key").GetString());
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task RequestCommand_Explain_WritesRequestSummary()
    {
        var args = new[]
        {
            "request", "GET", "/rest/api/3/myself",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--query", "expand=groups",
            "--explain"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.Contains("\"method\": \"GET\"", stdout);
        Assert.Contains("\"path\": \"/rest/api/3/myself\"", stdout);
    }

    [Fact]
    public async Task RequestCommand_Replay_UsesRecordedPayload()
    {
        var bodyFile = WriteTempFile("{\"name\":\"replay-test\"}");
        var replayFile = WriteTempFile(string.Empty);
        try
        {
            var explainArgs = new[]
            {
                "request", "POST", "/rest/api/3/project",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--query", "expand=description",
                "--header", "X-Test=1",
                "--in", bodyFile,
                "--input-format", "json",
                "--request-file", replayFile,
                "--explain",
                "--yes"
            };

            var (explainExitCode, _, _) = await InvokeProgramAsync(explainArgs);
            Assert.Equal(0, explainExitCode);

            var recorded = JsonSerializer.Deserialize<RecordedRequest>(File.ReadAllText(replayFile));
            Assert.NotNull(recorded);
            Assert.Equal("POST", recorded!.Method);
            Assert.Equal("/rest/api/3/project", recorded.Path);
            Assert.Contains(recorded.Query, q => q.Key == "expand" && q.Value == "description");
            Assert.Contains(recorded.Headers, h => h.Key == "X-Test" && h.Value == "1");
            Assert.Equal("{\"name\":\"replay-test\"}", recorded.Body);

            await using var server = new LocalReplayServer();
            server.EnqueueResponse(new ReplayResponse(
                200,
                "OK",
                new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                "{\"ok\":true}"));
            await server.StartAsync();

            var replayArgs = new[]
            {
                "request", "--replay", replayFile,
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--yes"
            };

            var (replayExitCode, stdout, _) = await InvokeProgramAsync(replayArgs);
            Assert.Equal(0, replayExitCode);
            Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(bodyFile);
            File.Delete(replayFile);
        }
    }

    [Fact]
    public async Task CapabilitiesCommand_EmitsExitCodeContract()
    {
        var (exitCode, stdout, _) = await InvokeProgramAsync(["capabilities"]);
        Assert.Equal(0, exitCode);
        Assert.Contains("\"exitCodes\"", stdout);
        Assert.Contains("\"10\": \"Internal / tool-specific\"", stdout);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("request --help")]
    [InlineData("issue create --help")]
    [InlineData("issue update --help")]
    [InlineData("issue comment add --help")]
    [InlineData("config init --help")]
    [InlineData("openapi --help")]
    [InlineData("doctor --help")]
    [InlineData("schema --help")]
    public async Task HelpOutput_HasNoDuplicateOptionRows(string commandLine)
    {
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);

        var optionRows = Regex.Matches(stdout, @"^\s{2,}--[a-z0-9-]+", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(m => m.Value.Trim())
            .ToList();

        var duplicate = optionRows
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        Assert.True(duplicate is null, $"Duplicate option row found: {duplicate?.Key}");
    }

    [Theory]
    [InlineData("request --help")]
    [InlineData("issue create --help")]
    [InlineData("issue update --help")]
    [InlineData("issue transition --help")]
    [InlineData("issue comment add --help")]
    [InlineData("issue comment update --help")]
    [InlineData("issuelink --help")]
    public async Task HelpOutput_IncludesBodyPayloadOptions(string commandLine)
    {
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.Contains("--body", stdout);
        Assert.Contains("--body-file", stdout);
    }

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

[CollectionDefinition("ProgramE2e", DisableParallelization = true)]
public sealed class ProgramE2eCollection;

internal sealed record ReplayResponse(int StatusCode, string ReasonPhrase, IReadOnlyDictionary<string, string> Headers, string Body);
internal sealed record ReplayNameValue(string Key, string Value);
internal sealed record RecordedRequest(string Method, string Path, List<ReplayNameValue> Query, List<ReplayNameValue> Headers, string Accept, string? ContentType, string? Body);
internal sealed record ReplayRequest(string Method, string Path, string Body, IReadOnlyDictionary<string, string> Headers);

internal sealed class LocalReplayServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
    private readonly Queue<ReplayResponse> responses = new();
    private readonly List<ReplayRequest> requests = [];
    private readonly CancellationTokenSource cts = new();
    private readonly object sync = new();
    private Task? loopTask;

    public string BaseUrl { get; private set; } = string.Empty;
    public ReplayRequest? LastRequest
    {
        get
        {
            lock (sync)
            {
                return requests.Count == 0 ? null : requests[^1];
            }
        }
    }

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
        using var clientHandle = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        var requestParts = (requestLine ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var method = requestParts.Length > 0 ? requestParts[0] : string.Empty;
        var path = requestParts.Length > 1 ? requestParts[1] : string.Empty;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null || line.Length == 0)
            {
                break;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            headers[key] = value;
        }

        var body = string.Empty;
        if (headers.TryGetValue("Content-Length", out var contentLengthRaw)
            && int.TryParse(contentLengthRaw, out var contentLength)
            && contentLength > 0)
        {
            var bodyBuffer = new char[contentLength];
            var read = 0;
            while (read < contentLength)
            {
                var count = await reader.ReadBlockAsync(bodyBuffer.AsMemory(read, contentLength - read), cancellationToken);
                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            body = new string(bodyBuffer, 0, read);
        }

        lock (sync)
        {
            requests.Add(new ReplayRequest(method, path, body, headers));
        }

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

        await writer.WriteLineAsync("Connection: close");
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
