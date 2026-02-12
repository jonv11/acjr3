namespace Acjr3.Http;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}
