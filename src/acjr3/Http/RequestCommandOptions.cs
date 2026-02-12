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
