namespace Acjr3.Common;

public static class RequestOptionParser
{
    public static bool TryParsePairs(string[] raw, out List<KeyValuePair<string, string>> pairs, out string error)
    {
        pairs = [];
        error = string.Empty;

        foreach (var item in raw)
        {
            var idx = item.IndexOf('=');
            if (idx <= 0 || idx == item.Length - 1)
            {
                error = $"Invalid key=value input: '{item}'";
                return false;
            }

            var key = item[..idx].Trim();
            var value = item[(idx + 1)..].Trim();
            if (key.Length == 0)
            {
                error = $"Invalid key=value input: '{item}'";
                return false;
            }

            pairs.Add(new KeyValuePair<string, string>(key, value));
        }

        return true;
    }
}
