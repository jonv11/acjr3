using System.Net;
using Acjr3;

namespace Acjr3.Tests.Http;

public sealed class RetryPolicyTests
{
    [Fact]
    public void ShouldRetryResponse_TrueFor429_FalseFor400()
    {
        var policy = new RetryPolicy(new FakeClock());

        Assert.True(policy.ShouldRetryResponse(new HttpResponseMessage((HttpStatusCode)429)));
        Assert.False(policy.ShouldRetryResponse(new HttpResponseMessage(HttpStatusCode.BadRequest)));
    }

    [Fact]
    public void IsMethodRetryable_OnlyIdempotentByDefault()
    {
        var policy = new RetryPolicy(new FakeClock());

        Assert.True(policy.IsMethodRetryable(HttpMethod.Get, retryNonIdempotent: false));
        Assert.False(policy.IsMethodRetryable(HttpMethod.Post, retryNonIdempotent: false));
        Assert.True(policy.IsMethodRetryable(HttpMethod.Post, retryNonIdempotent: true));
    }

    [Fact]
    public async Task WaitAsync_UsesRetryAfterHeader_WhenPresent()
    {
        var clock = new FakeClock();
        var policy = new RetryPolicy(clock);
        var config = new Acjr3Config(
            new Uri("https://example.atlassian.net"),
            AuthMode.Basic,
            "u",
            "t",
            null,
            100,
            5,
            500);

        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.TryAddWithoutValidation("Retry-After", "2");

        await policy.WaitAsync(response, 1, config, new TestLogger(), CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(2), clock.LastDelay);
    }
}

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TimeSpan LastDelay { get; private set; }

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        LastDelay = delay;
        return Task.CompletedTask;
    }
}

public sealed class TestLogger : IAppLogger
{
    public bool IsVerbose => false;

    public void Verbose(string message)
    {
    }
}


