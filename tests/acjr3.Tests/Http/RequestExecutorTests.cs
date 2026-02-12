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

