using System.Net;
using System.Text;
using Acjr3.Output;

namespace Acjr3.Tests.Http;

public sealed class RequestExecutorTests
{
    private static readonly SemaphoreSlim ConsoleSemaphore = new(1, 1);

    [Fact]
    public async Task ExecuteAsync_RetriesOn429_AndHonorsRetryAfter()
    {
        var clock = new FakeClock();
        var handler = new SequenceHttpMessageHandler(
            (_, _) =>
            {
                var response = JsonResponse((HttpStatusCode)429, "{\"error\":\"rate-limited\"}");
                response.Headers.TryAddWithoutValidation("Retry-After", "2");
                return Task.FromResult(response);
            },
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));

        var executor = CreateExecutor(handler, clock);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, _, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 2), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(2), clock.LastDelay);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOn5xx()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}")),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));

        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, _, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 2), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnHttpRequestException()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => throw new HttpRequestException("network"),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));

        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, _, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 2), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnTaskCanceledException()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => throw new TaskCanceledException("timeout"),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));

        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project");

        var (exitCode, _, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 2), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Paginate_UsesIsLastFlow()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"startAt\":0,\"maxResults\":1,\"isLast\":false,\"values\":[{\"id\":\"1\"}]}")),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"startAt\":1,\"maxResults\":1,\"isLast\":true,\"values\":[{\"id\":\"2\"}]}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project", paginate: true);

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 2), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("\"id\":\"1\"", stdout);
        Assert.Contains("\"id\":\"2\"", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_Paginate_UsesTotalFlow()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"startAt\":0,\"maxResults\":1,\"total\":2,\"values\":[{\"id\":\"1\"}]}")),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"startAt\":1,\"maxResults\":1,\"total\":2,\"values\":[{\"id\":\"2\"}]}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project", paginate: true);

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 2), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("\"id\":\"1\"", stdout);
        Assert.Contains("\"id\":\"2\"", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_Paginate_StopsOnEmptyValuesWhenNoIsLastOrTotal()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"startAt\":0,\"maxResults\":2,\"values\":[{\"id\":\"1\"}]}")),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"startAt\":2,\"maxResults\":2,\"values\":[]}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project", paginate: true);

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(maxRetries: 2), options);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("\"id\":\"1\"", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_FailOnNonSuccess_ReturnsOne()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, "{\"error\":\"bad\"}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project", failOnNonSuccess: true);

        var (exitCode, _, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_OutPath_StreamsBodyToFile()
    {
        var bytes = Encoding.UTF8.GetBytes("file-content");
        var handler = new SequenceHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(response);
        });

        var executor = CreateExecutor(handler);
        var outPath = Path.Combine(Path.GetTempPath(), $"acjr3-request-out-{Guid.NewGuid():N}.bin");
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project", outPath: outPath, includeHeaders: true);

        try
        {
            var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

            Assert.Equal(0, exitCode);
            Assert.Contains("HTTP 200 OK", stdout);
            Assert.Contains("Saved response body to", stdout);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(outPath));
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
    public async Task ExecuteAsync_RawFalse_PrettyPrintsJson()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"name\":\"v\"}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project", raw: false);

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Contains($"{Environment.NewLine}  \"name\": \"v\"", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_RawTrue_OutputsCompactJson()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"name\":\"v\"}")));
        var executor = CreateExecutor(handler);
        var options = CreateOptions(HttpMethod.Get, "/rest/api/3/project", raw: true);

        var (exitCode, stdout, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Contains("{\"name\":\"v\"}", stdout);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsUserProvidedContentTypeHeader()
    {
        var seenContentType = string.Empty;
        var handler = new SequenceHttpMessageHandler((request, _) =>
        {
            seenContentType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"ok\":true}"));
        });

        var executor = CreateExecutor(handler);
        var options = CreateOptions(
            HttpMethod.Post,
            "/rest/api/3/project",
            body: "{\"name\":\"x\"}",
            headers: [new KeyValuePair<string, string>("Content-Type", "text/plain")]);

        var (exitCode, _, _) = await ExecuteAndCaptureAsync(executor, CreateConfig(), options);

        Assert.Equal(0, exitCode);
        Assert.Equal("text/plain", seenContentType);
    }

    private static RequestExecutor CreateExecutor(HttpMessageHandler handler, IClock? clock = null)
    {
        var factory = new TestHttpClientFactory(handler);
        var retry = new RetryPolicy(clock ?? new FakeClock());
        return new RequestExecutor(factory, new AuthHeaderProvider(), retry, new ResponseFormatter());
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
        bool raw = true,
        bool includeHeaders = false,
        bool failOnNonSuccess = false,
        bool paginate = false,
        string? body = null,
        string? outPath = null,
        IReadOnlyList<KeyValuePair<string, string>>? headers = null,
        IReadOnlyList<KeyValuePair<string, string>>? query = null)
        => new(
            method,
            path,
            query ?? [],
            headers ?? [],
            "application/json",
            null,
            body,
            outPath,
            raw,
            includeHeaders,
            failOnNonSuccess,
            DryRun: false,
            RetryNonIdempotent: true,
            paginate);

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

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("No scripted response available.");
        }

        return steps.Dequeue()(request, cancellationToken);
    }
}
