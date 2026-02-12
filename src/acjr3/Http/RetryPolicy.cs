using System.Net;

namespace Acjr3.Http;

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
