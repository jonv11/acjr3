using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Http;

public sealed record RequestCommandOptions(
    HttpMethod Method,
    string Path,
    IReadOnlyList<KeyValuePair<string, string>> Query,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string Accept,
    string? ContentType,
    string? Body,
    string? OutPath,
    OutputPreferences Output,
    bool FailOnNonSuccess,
    bool RetryNonIdempotent,
    bool Paginate,
    bool Confirmed);

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}

public sealed class RetryPolicy(IClock clock)
{
    private const int MaxBackoffMs = 30000;

    public bool IsMethodRetryable(HttpMethod method, bool retryNonIdempotent)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Put || method == HttpMethod.Delete)
        {
            return true;
        }

        return retryNonIdempotent;
    }

    public bool ShouldRetryResponse(HttpResponseMessage response)
        => response.StatusCode == (HttpStatusCode)429 || (int)response.StatusCode >= 500;

    public bool ShouldRetryException(Exception ex)
        => ex is HttpRequestException || ex is TaskCanceledException;

    public async Task WaitAsync(HttpResponseMessage? response, int attempt, Acjr3Config config, IAppLogger logger, CancellationToken cancellationToken)
    {
        var delay = ComputeDelay(response, attempt, config);
        logger.Verbose($"Retry wait {delay.TotalMilliseconds:F0}ms before attempt {attempt + 1}");
        await clock.Delay(delay, cancellationToken);
    }

    internal TimeSpan ComputeDelay(HttpResponseMessage? response, int attempt, Acjr3Config config)
    {
        if (response?.StatusCode == (HttpStatusCode)429 && TryGetRetryAfter(response, out var retryAfter))
        {
            return retryAfter;
        }

        var exp = config.RetryBaseDelayMs * Math.Pow(2, Math.Max(0, attempt - 1));
        var jitter = Random.Shared.Next(0, 250);
        var millis = Math.Min(MaxBackoffMs, exp + jitter);
        return TimeSpan.FromMilliseconds(millis);
    }

    private TimeSpan ClampPositive(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var max = TimeSpan.FromMilliseconds(MaxBackoffMs);
        return value > max ? max : value;
    }

    private bool TryGetRetryAfter(HttpResponseMessage response, out TimeSpan delay)
    {
        delay = default;

        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            delay = ClampPositive(delta);
            return true;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            delay = ClampPositive(date - clock.UtcNow);
            return true;
        }

        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var first = values.FirstOrDefault();
            if (int.TryParse(first, out var seconds))
            {
                delay = ClampPositive(TimeSpan.FromSeconds(seconds));
                return true;
            }
        }

        return false;
    }
}

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
                logger.Verbose($"Retryable exception: {ex.GetType().Name} - {ex.Message}");
                await retryPolicy.WaitAsync(null, attempt, config, logger, cancellationToken);
            }
        }

        throw new HttpRequestException($"Request failed after {maxAttempts} attempts.", lastException);
    }

    private static HttpRequestMessage BuildRequestMessage(HttpMethod method, Uri url, Dictionary<string, string> headers, string? body)
    {
        var request = new HttpRequestMessage(method, url);
        foreach (var item in headers)
        {
            if (item.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(item.Key, item.Value);
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            var contentType = headers.TryGetValue("Content-Type", out var ct) ? ct : "application/json";
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
        using var _ = response;
        stopwatch.Stop();

        var statusCode = response.StatusCode;
        var requestId = TryGetRequestId(response);
        var meta = new CliMeta(
            Version: EnvelopeVersion,
            RequestId: requestId,
            DurationMs: (long)stopwatch.Elapsed.TotalMilliseconds,
            StatusCode: (int)statusCode,
            Method: options.Method.Method,
            Path: options.Path);

        if (!string.IsNullOrWhiteSpace(options.OutPath))
        {
            var fileInfo = await SaveBodyToFileAsync(response, options.OutPath!, cancellationToken);
            var envelope = response.IsSuccessStatusCode
                ? new CliEnvelope(true, fileInfo, null, meta)
                : new CliEnvelope(false, null, BuildHttpError(statusCode, response.ReasonPhrase, null), meta);

            WriteEnvelope(envelope, options.Output);
            if (response.IsSuccessStatusCode)
            {
                return (int)CliExitCode.Success;
            }

            if (!options.FailOnNonSuccess)
            {
                return (int)CliExitCode.Success;
            }

            var (exitCode, _) = CliErrorMapper.FromHttpStatus(statusCode);
            return (int)exitCode;
        }

        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var parsedData = ParsePayload(response, payload);

        if (response.IsSuccessStatusCode)
        {
            var envelope = new CliEnvelope(true, parsedData, null, meta);
            WriteEnvelope(envelope, options.Output);
            return (int)CliExitCode.Success;
        }

        var error = BuildHttpError(statusCode, response.ReasonPhrase, parsedData);
        var errorEnvelope = new CliEnvelope(false, null, error, meta);
        WriteEnvelope(errorEnvelope, options.Output);

        if (!options.FailOnNonSuccess)
        {
            return (int)CliExitCode.Success;
        }

        var (mappedExitCode, _) = CliErrorMapper.FromHttpStatus(statusCode);
        return (int)mappedExitCode;
    }

    private static async Task<object> SaveBodyToFileAsync(HttpResponseMessage response, string outPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fileStream, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);
        return new
        {
            savedTo = outPath,
            bytes = fileStream.Length
        };
    }

    private static CliError BuildHttpError(HttpStatusCode statusCode, string? reasonPhrase, object? details)
    {
        var (_, mappedCode) = CliErrorMapper.FromHttpStatus(statusCode);
        var message = $"HTTP {(int)statusCode} {reasonPhrase}".Trim();
        return new CliError(mappedCode, message, details, "Use --trace for request/response diagnostics.");
    }

    private static object? ParsePayload(HttpResponseMessage response, byte[] payload)
    {
        if (payload.Length == 0 || response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(payload);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var probablyJson = mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || text.TrimStart().StartsWith('{')
            || text.TrimStart().StartsWith('[');

        if (!probablyJson)
        {
            return text;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch
        {
            return text;
        }
    }

    private static string? TryGetRequestId(HttpResponseMessage response)
    {
        foreach (var headerName in new[] { "X-Request-Id", "x-request-id", "X-Arequestid", "x-arequestid" })
        {
            if (response.Headers.TryGetValues(headerName, out var values))
            {
                return values.FirstOrDefault();
            }
        }

        return null;
    }

    private void WriteEnvelope(CliEnvelope envelope, OutputPreferences preferences)
    {
        var text = preferences.Format == OutputFormat.Text
            ? outputRenderer.RenderText(envelope, preferences)
            : outputRenderer.RenderEnvelope(envelope, preferences);
        if (!string.IsNullOrWhiteSpace(text))
        {
            Console.Out.WriteLine(text);
        }
    }

    private async Task<int> ExecutePaginatedAsync(
        Acjr3Config config,
        RequestCommandOptions options,
        IAppLogger logger,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        var accumulated = new List<JsonElement>();
        JsonElement? rootTemplate = null;

        var query = options.Query.ToDictionary(q => q.Key, q => q.Value, StringComparer.OrdinalIgnoreCase);
        var startAt = query.TryGetValue("startAt", out var startAtRaw) && int.TryParse(startAtRaw, out var parsed) ? parsed : 0;

        while (true)
        {
            query["startAt"] = startAt.ToString();
            var pageOptions = options with { Query = query.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).ToList() };
            var url = UrlBuilder.Build(config.SiteUrl, pageOptions.Path, pageOptions.Query);
            var headers = BuildHeaders(config, pageOptions);
            var response = await SendWithRetriesAsync(config, pageOptions, url, headers, logger, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await HandleResponseAsync(response, options with { Paginate = false }, cancellationToken, stopwatch);
            }

            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (rootTemplate is null)
            {
                rootTemplate = root.Clone();
            }

            if (!TryExtractPage(root, accumulated, out var nextStartAt, out var done, out var fallbackMessage))
            {
                response.Dispose();
                return WriteValidationError(fallbackMessage, options.Output);
            }

            response.Dispose();
            if (done)
            {
                break;
            }

            startAt = nextStartAt;
        }

        var combined = BuildCombinedOutput(rootTemplate!.Value, accumulated);
        using var synthetic = new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(combined, Encoding.UTF8, "application/json")
        };

        return await HandleResponseAsync(synthetic, options with { Paginate = false }, cancellationToken, stopwatch);
    }

    private static bool TryExtractPage(JsonElement root, List<JsonElement> accumulated, out int nextStartAt, out bool done, out string message)
    {
        nextStartAt = 0;
        done = true;
        message = string.Empty;

        if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            message = "Pagination structure not recognized (missing values array).";
            return false;
        }

        foreach (var item in values.EnumerateArray())
        {
            accumulated.Add(item.Clone());
        }

        var hasIsLast = root.TryGetProperty("isLast", out var isLastProp)
            && (isLastProp.ValueKind == JsonValueKind.True || isLastProp.ValueKind == JsonValueKind.False);
        var isLast = hasIsLast && isLastProp.GetBoolean();

        var startAt = root.TryGetProperty("startAt", out var startAtProp) && startAtProp.TryGetInt32(out var st) ? st : 0;
        var maxResults = root.TryGetProperty("maxResults", out var maxProp) && maxProp.TryGetInt32(out var mx) ? mx : values.GetArrayLength();
        int? totalValue = null;
        if (root.TryGetProperty("total", out var totalProp) && totalProp.TryGetInt32(out var parsedTotal))
        {
            totalValue = parsedTotal;
        }

        nextStartAt = startAt + Math.Max(1, maxResults);

        if (hasIsLast)
        {
            done = isLast;
            return true;
        }

        if (totalValue.HasValue)
        {
            done = nextStartAt >= totalValue.Value;
            return true;
        }

        done = values.GetArrayLength() == 0;
        return true;
    }

    private static string BuildCombinedOutput(JsonElement rootTemplate, List<JsonElement> items)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        foreach (var property in rootTemplate.EnumerateObject())
        {
            if (property.NameEquals("values"))
            {
                writer.WritePropertyName("values");
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    item.WriteTo(writer);
                }

                writer.WriteEndArray();
                continue;
            }

            property.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

