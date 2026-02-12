namespace Acjr3.Common;

public static class HttpMethodParser
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH"
    };

    public static bool TryParse(string raw, out HttpMethod? method, out string error)
    {
        method = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw) || !Allowed.Contains(raw))
        {
            error = "METHOD must be one of: GET, POST, PUT, DELETE, PATCH.";
            return false;
        }

        method = new HttpMethod(raw.ToUpperInvariant());
        return true;
    }
}
