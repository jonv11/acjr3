using System.Text.Json.Serialization;

namespace Acjr3.Common;

public enum CliExitCode
{
    Success = 0,
    Validation = 1,
    Authentication = 2,
    NotFound = 3,
    Conflict = 4,
    Network = 5,
    Internal = 10
}

public static class CliErrorCode
{
    public const string Validation = "validation_error";
    public const string Authentication = "authentication_error";
    public const string Authorization = "authorization_error";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string Network = "network_error";
    public const string Timeout = "timeout";
    public const string Internal = "internal_error";
    public const string Upstream = "upstream_error";
}

public sealed record CliError(
    [property: JsonPropertyName("code")]
    string Code,
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("details")]
    object? Details,
    [property: JsonPropertyName("hint")]
    string? Hint);

public sealed record CliMeta(
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("requestId")]
    string? RequestId,
    [property: JsonPropertyName("durationMs")]
    long? DurationMs,
    [property: JsonPropertyName("statusCode")]
    int? StatusCode,
    [property: JsonPropertyName("method")]
    string? Method,
    [property: JsonPropertyName("path")]
    string? Path);

public sealed record CliEnvelope(
    [property: JsonPropertyName("success")]
    bool Success,
    [property: JsonPropertyName("data")]
    object? Data,
    [property: JsonPropertyName("error")]
    CliError? Error,
    [property: JsonPropertyName("meta")]
    CliMeta Meta);
