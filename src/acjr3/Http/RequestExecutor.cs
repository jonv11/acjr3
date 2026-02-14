using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Http;

public sealed class RequestExecutor(
    IHttpClientFactory httpClientFactory,
    AuthHeaderProvider authHeaderProvider,
    RetryPolicy retryPolicy,
    OutputRenderer outputRenderer)
{
    private const string EnvelopeVersion = "1.0";

    public async Task<int> ExecuteAsync(
        Acjr3Config config,
        RequestCommandOptions options,
        IAppLogger logger,
        CancellationToken cancellationToken)
    {
        if (options.Paginate && options.Method != HttpMethod.Get)
        {
            return WriteValidationError("--all/--paginate is only supported for GET requests.", options.Output);
        }

        if (IsMutatingMethod(options.Method) && !options.Confirmed)
        {
            return WriteValidationError("Destructive/mutating operation requires --yes or --force.", options.Output);
        }

        var url = UrlBuilder.Build(config.SiteUrl, options.Path, options.Query);
        var headers = BuildHeaders(config, options);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (options.Paginate)
            {
                return await ExecutePaginatedAsync(config, options, logger, cancellationToken, stopwatch);
            }

            var response = await SendWithRetriesAsync(config, options, url, headers, logger, cancellationToken);
            return await HandleResponseAsync(response, options, cancellationToken, stopwatch);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var (exitCode, errorCode) = CliErrorMapper.FromException(ex);
            var envelope = new CliEnvelope(
                Success: false,
                Data: null,
                Error: new CliError(errorCode, ex.Message, null, "Inspect --debug/--trace output and verify network connectivity."),
                Meta: new CliMeta(
                    EnvelopeVersion,
                    RequestId: null,
                    DurationMs: (long)stopwatch.Elapsed.TotalMilliseconds,
                    StatusCode: null,
                    Method: options.Method.Method,
                    Path: options.Path));
            WriteEnvelope(envelope, options.Output);
            return (int)exitCode;
        }
    }

    private static bool IsMutatingMethod(HttpMethod method)
    {
        return method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch || method == HttpMethod.Delete;
    }

    private int WriteValidationError(string message, OutputPreferences output)
    {
        var envelope = new CliEnvelope(
            Success: false,
            Data: null,
            Error: new CliError(CliErrorCode.Validation, message, null, null),
            Meta: new CliMeta(EnvelopeVersion, null, null, null, null, null));
        WriteEnvelope(envelope, output);
        return (int)CliExitCode.Validation;
    }

    private Dictionary<string, string> BuildHeaders(Acjr3Config config, RequestCommandOptions options)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = options.Accept,
            ["User-Agent"] = $"acjr3/{typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"}"
        };

        var auth = authHeaderProvider.Create(config);
        headers["Authorization"] = $"{auth.Scheme} {auth.Parameter}";

        foreach (var pair in options.Headers)
        {
            headers[pair.Key] = pair.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.Body))
        {
            if (!string.IsNullOrWhiteSpace(options.ContentType))
            {
                headers["Content-Type"] = options.ContentType!;
            }
            else if (!headers.ContainsKey("Content-Type"))
            {
                headers["Content-Type"] = "application/json";
            }
        }

        return headers;
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(
        Acjr3Config config,
        RequestCommandOptions options,
        Uri url,
        Dictionary<string, string> headers,
        IAppLogger logger,
        CancellationToken cancellationToken)
    {
        var canRetry = retryPolicy.IsMethodRetryable(options.Method, options.RetryNonIdempotent);
        var maxAttempts = Math.Max(1, config.MaxRetries + 1);

        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = BuildRequestMessage(options.Method, url, headers, options.Body);
                logger.Verbose($"Sending {options.Method} {url} attempt={attempt}/{maxAttempts}");

                var client = httpClientFactory.CreateClient("acjr3");
                client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!retryPolicy.ShouldRetryResponse(response) || !canRetry || attempt == maxAttempts)
                {
                    return response;
                }

                logger.Verbose($"Retryable response {(int)response.StatusCode} {response.ReasonPhrase}");
                await retryPolicy.WaitAsync(response, attempt, config, logger, cancellationToken);
                response.Dispose();
            }
            catch (Exception ex) when (retryPolicy.ShouldRetryException(ex) && canRetry && attempt < maxAttempts)
            {
                lastException = ex;
                logger.Verbose($"Retryable exception {ex.GetType().Name}: {ex.Message}");
                await retryPolicy.WaitAsync(response: null, attempt, config, logger, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Request failed after retries.");
    }

    private static HttpRequestMessage BuildRequestMessage(HttpMethod method, Uri url, Dictionary<string, string> headers, string? body)
    {
        var request = new HttpRequestMessage(method, url);
        foreach (var header in headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            var contentType = headers.TryGetValue("Content-Type", out var type) ? type : "application/json";
            request.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        return request;
    }

    private async Task<int> HandleResponseAsync(
        HttpResponseMessage response,
        RequestCommandOptions options,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        using var _ = response.Content;
        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        stopwatch.Stop();

        object? data;
        if (!string.IsNullOrWhiteSpace(options.OutPath))
        {
            data = await RequestResponseHelpers.SaveBodyToFileAsync(response, options.OutPath!, cancellationToken);
        }
        else
        {
            data = RequestResponseHelpers.ParsePayload(response, payload);
        }

        var requestId = RequestResponseHelpers.TryGetRequestId(response);
        var meta = new CliMeta(
            EnvelopeVersion,
            requestId,
            (long)stopwatch.Elapsed.TotalMilliseconds,
            (int)response.StatusCode,
            options.Method.Method,
            options.Path);

        if (!response.IsSuccessStatusCode)
        {
            var (exitCode, errorCode) = CliErrorMapper.FromHttpStatus(response.StatusCode);
            var error = RequestResponseHelpers.BuildHttpError(response.StatusCode, response.ReasonPhrase, data);
            var envelope = new CliEnvelope(false, null, error with { Code = errorCode }, meta);
            WriteEnvelope(envelope, options.Output);
            return options.FailOnNonSuccess ? (int)exitCode : 0;
        }

        var successEnvelope = new CliEnvelope(true, data, null, meta);
        WriteEnvelope(successEnvelope, options.Output);

        return 0;
    }

    private void WriteEnvelope(CliEnvelope envelope, OutputPreferences preferences)
    {
        var text = preferences.Format == OutputFormat.Text
            ? outputRenderer.RenderText(envelope, preferences)
            : outputRenderer.RenderEnvelope(envelope, preferences);
        Console.Out.WriteLine(text);
    }

    private async Task<int> ExecutePaginatedAsync(
        Acjr3Config config,
        RequestCommandOptions options,
        IAppLogger logger,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        var accumulated = new List<JsonElement>();
        var rootTemplate = default(JsonElement);
        var nextStartAt = 0;

        while (true)
        {
            var query = options.Query
                .Where(p => !p.Key.Equals("startAt", StringComparison.OrdinalIgnoreCase))
                .ToList();
            query.Add(new KeyValuePair<string, string>("startAt", nextStartAt.ToString()));

            var pageOptions = options with { Query = query };
            var url = UrlBuilder.Build(config.SiteUrl, pageOptions.Path, pageOptions.Query);
            var headers = BuildHeaders(config, pageOptions);
            using var response = await SendWithRetriesAsync(config, pageOptions, url, headers, logger, cancellationToken);
            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await HandleResponseAsync(response, options, cancellationToken, stopwatch);
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement.Clone();
            if (rootTemplate.ValueKind == JsonValueKind.Undefined)
            {
                rootTemplate = root;
            }

            if (!RequestResponseHelpers.TryExtractPage(root, accumulated, out var newStartAt, out var isLast))
            {
                break;
            }

            if (isLast)
            {
                break;
            }

            nextStartAt = newStartAt;
        }

        stopwatch.Stop();

        var combined = RequestResponseHelpers.BuildCombinedOutput(rootTemplate, accumulated);
        var envelope = new CliEnvelope(
            true,
            JsonDocument.Parse(combined).RootElement.Clone(),
            null,
            new CliMeta(EnvelopeVersion, null, (long)stopwatch.Elapsed.TotalMilliseconds, 200, options.Method.Method, options.Path));
        WriteEnvelope(envelope, options.Output);
        return 0;
    }

}
