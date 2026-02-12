namespace Acjr3.Output;

public enum OutputFormat
{
    Json,
    Jsonl,
    Text
}

public enum JsonStyle
{
    Pretty,
    Compact
}

public sealed record OutputPreferences(
    OutputFormat Format,
    JsonStyle JsonStyle,
    string? Select,
    string? Filter,
    string? Sort,
    int? Limit,
    string? Cursor,
    int? Page,
    bool All,
    bool Plain)
{
    public static OutputPreferences Default { get; } = new(
        OutputFormat.Json,
        JsonStyle.Pretty,
        Select: null,
        Filter: null,
        Sort: null,
        Limit: null,
        Cursor: null,
        Page: null,
        All: false,
        Plain: false);
}
