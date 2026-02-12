namespace Acjr3.Configuration;

public sealed record Acjr3Config(
    Uri SiteUrl,
    AuthMode AuthMode,
    string? Email,
    string? ApiToken,
    string? BearerToken,
    int TimeoutSeconds,
    int MaxRetries,
    int RetryBaseDelayMs);
