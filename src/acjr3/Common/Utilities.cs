using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Acjr3.Common;

public interface IAppLogger
{
    bool IsVerbose { get; }

    void Verbose(string message);
}

public sealed class ConsoleLogger(bool isVerbose) : IAppLogger
{
    private readonly ILogger logger = CreateLogger();

    private static ILoggerFactory? loggerFactory;

    public bool IsVerbose { get; } = isVerbose;

    public static void SetLoggerFactory(ILoggerFactory factory)
    {
        loggerFactory = factory;
    }

    public void Verbose(string message)
    {
        if (IsVerbose)
        {
            logger.LogDebug("{VerboseMessage}", message);
        }
    }

    private static ILogger CreateLogger()
    {
        if (loggerFactory != null)
        {
            return loggerFactory.CreateLogger("Acjr3.Cli");
        }

        // Fallback for unit tests or execution paths where DI has not been initialized.
        return LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                });
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddFilter("Acjr3", LogLevel.Debug);
            })
            .CreateLogger("Acjr3.Cli");
    }
}

public static class Redactor
{
    public static string RedactHeader(string key, string value)
    {
        if (key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            return "<redacted>";
        }

        return value;
    }

    public static string MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<not set>";
        }

        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return $"{value[..2]}***{value[^2..]}";
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "<not set>";
        }

        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        return $"{email[0]}***{email[(at - 1)..]}";
    }
}

public sealed class AuthHeaderProvider
{
    public AuthenticationHeaderValue Create(Acjr3Config config)
    {
        return config.AuthMode switch
        {
            AuthMode.Basic => BuildBasic(config.Email!, config.ApiToken!),
            AuthMode.Bearer => new AuthenticationHeaderValue("Bearer", config.BearerToken),
            _ => throw new InvalidOperationException($"Unsupported auth mode {config.AuthMode}")
        };
    }

    private static AuthenticationHeaderValue BuildBasic(string email, string token)
    {
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
        return new AuthenticationHeaderValue("Basic", value);
    }
}

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


