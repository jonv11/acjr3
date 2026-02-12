namespace Acjr3.Common;

public sealed record StoredRequest(
    string Method,
    string Path,
    IReadOnlyList<KeyValuePair<string, string>> Query,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string Accept,
    string? ContentType,
    string? Body);
