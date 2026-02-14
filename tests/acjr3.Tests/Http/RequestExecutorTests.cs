using System.Net;
using System.Text;
using Acjr3.Output;

namespace Acjr3.Tests.Http;

public sealed class RequestExecutorTests
{
    private static readonly SemaphoreSlim ConsoleSemaphore = new(1, 1);

    [Fact]
    public async Task ExecuteAsync_Success_WritesEnvelopeJson()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"ok\": true", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_NonSuccess_MapsExitCode()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"missing\"}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal((int)CliExitCode.NotFound, exitCode);
        Assert.Contains("\"Success\": false", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NonSuccess_WithAllowNonSuccess_ReturnsZero()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"missing\"}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project") with { FailOnNonSuccess = false };

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Success\": false", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MutatingWithoutConfirmation_FailsValidation()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Post, "/rest/api/3/project", body: "{\"name\":\"x\"}", confirmed: false);

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal((int)CliExitCode.Validation, exitCode);
        Assert.Contains("requires --yes or --force", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_WithOutPath_WritesResponseBodyToFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "acjr3-tests", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(outputDir, "response.json");
        try
        {
            var handler = new SequenceHttpMessageHandler(
                (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"saved\":true}")));
            var executor = CreateExecutor(handler);
            var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project") with { OutPath = outputPath };

            var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Equal("{\"saved\":true}", File.ReadAllText(outputPath));
            Assert.Contains("\"file\":", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_JsonContentTypeWithInvalidJson_FallsBackToString()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "application/json")
            }));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Contains("not-json", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ResponseIncludesRequestId_InMeta()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) =>
            {
                var response = JsonResponse(HttpStatusCode.OK, "{\"ok\":true}");
                response.Headers.TryAddWithoutValidation("x-arequestid", "req-123");
                return Task.FromResult(response);
            });
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Contains("req-123", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Paginate_OnNonGet_ReturnsValidationExitCode()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Post, "/rest/api/3/project", body: "{\"name\":\"x\"}") with { Paginate = true };

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal((int)CliExitCode.Validation, exitCode);
        Assert.Contains("--all/--paginate is only supported for GET requests.", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_Paginate_AggregatesValuesAcrossPages()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                "{\"startAt\":0,\"maxResults\":1,\"total\":2,\"values\":[{\"id\":1}]}")),
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                "{\"startAt\":1,\"maxResults\":1,\"total\":2,\"values\":[{\"id\":2}]}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project/search") with { Paginate = true };

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"id\": 1", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"id\": 2", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RetryableException_RetriesThenSucceeds()
    {
        var attempts = 0;
        var handler = new SequenceHttpMessageHandler(
            (_, _) =>
            {
                attempts++;
                throw new HttpRequestException("transient");
            },
            (_, _) =>
            {
                attempts++;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}"));
            });
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 1), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, attempts);
        Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_TaskCanceled_MapsToTimeoutExitCode()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => throw new TaskCanceledException("timed out"));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 0), options);

        Assert.Equal((int)CliExitCode.Network, exitCode);
        Assert.Contains("timeout", stdout, StringComparison.OrdinalIgnoreCase);
    }

    private static RequestExecutor CreateExecutor(HttpMessageHandler handler, IClock? clock = null)
    {
        var factory = new TestHttpClientFactory(handler);
        var retry = new RetryPolicy(clock ?? new FakeClock());
        return new RequestExecutor(factory, new AuthHeaderProvider(), retry, new OutputRenderer());
    }

    private static Acjr3Config CreateConfig(int maxRetries = 1)
        => new(
            new Uri("https://example.atlassian.net"),
            AuthMode.Basic,
            "user@example.com",
            "token",
            null,
            30,
            maxRetries,
            10);

    private static RequestCommandOptions CreateOptions(
        HttpMethod method,
        string path,
        string? body = null,
        bool confirmed = true)
    {
        return new RequestCommandOptions(
            method,
            path,
            [],
            [],
            "application/json",
            null,
            body,
            null,
            OutputPreferences.Default,
            true,
            false,
            false,
            confirmed);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteAndCaptureAsync(
        RequestExecutor executor,
        Acjr3Config config,
        RequestCommandOptions options)
    {
        await ConsoleSemaphore.WaitAsync();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);

        try
        {
            var exitCode = await executor.ExecuteAsync(config, options, new TestLogger(), CancellationToken.None);
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            ConsoleSemaphore.Release();
        }
    }
}

internal sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class SequenceHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> steps;

    public SequenceHttpMessageHandler(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] steps)
    {
        this.steps = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(steps);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("No scripted response available.");
        }

        return steps.Dequeue()(request, cancellationToken);
    }
}
