namespace Acjr3.Common;

public static class UrlBuilder
{
    public static Uri Build(Uri baseUri, string path, IEnumerable<KeyValuePair<string, string>> query)
    {
        var normalizedPath = NormalizePath(path);
        var full = new Uri(baseUri.ToString().TrimEnd('/') + normalizedPath, UriKind.Absolute);

        var pairs = query.ToArray();
        if (pairs.Length == 0)
        {
            return full;
        }

        var builder = new UriBuilder(full);
        var queryText = string.Join("&", pairs.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        if (string.IsNullOrWhiteSpace(builder.Query))
        {
            builder.Query = queryText;
        }
        else
        {
            builder.Query = builder.Query.TrimStart('?') + "&" + queryText;
        }

        return builder.Uri;
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty", nameof(path));
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
