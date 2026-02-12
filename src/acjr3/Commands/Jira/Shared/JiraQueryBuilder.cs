namespace Acjr3.Commands.Jira;

internal static class JiraQueryBuilder
{
    public static void AddString(List<KeyValuePair<string, string>> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    public static void AddInt(List<KeyValuePair<string, string>> query, string key, int? value)
    {
        if (value.HasValue)
        {
            query.Add(new KeyValuePair<string, string>(key, value.Value.ToString()));
        }
    }

    public static void AddBoolean(List<KeyValuePair<string, string>> query, string key, bool? value)
    {
        if (value.HasValue)
        {
            query.Add(new KeyValuePair<string, string>(key, value.Value ? "true" : "false"));
        }
    }
}
