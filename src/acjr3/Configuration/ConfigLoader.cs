namespace Acjr3.Configuration;

public sealed class ConfigLoader(IEnvironmentSource source)
{
    public bool TryLoad(out Acjr3Config? config, out string error)
    {
        config = null;
        error = string.Empty;

        var siteUrlRaw = source.Get("ACJR3_SITE_URL");
        if (string.IsNullOrWhiteSpace(siteUrlRaw))
        {
            error = "ACJR3_SITE_URL is required.";
            return false;
        }

        if (!Uri.TryCreate(siteUrlRaw.Trim().TrimEnd('/'), UriKind.Absolute, out var siteUrl)
            || (siteUrl.Scheme != Uri.UriSchemeHttps && siteUrl.Scheme != Uri.UriSchemeHttp))
        {
            error = "ACJR3_SITE_URL must be a valid absolute http/https URL.";
            return false;
        }

        var authModeRaw = source.Get("ACJR3_AUTH_MODE");
        var authMode = AuthMode.Basic;
        if (!string.IsNullOrWhiteSpace(authModeRaw))
        {
            if (authModeRaw.Equals("basic", StringComparison.OrdinalIgnoreCase))
            {
                authMode = AuthMode.Basic;
            }
            else if (authModeRaw.Equals("bearer", StringComparison.OrdinalIgnoreCase))
            {
                authMode = AuthMode.Bearer;
            }
            else
            {
                error = "ACJR3_AUTH_MODE must be one of: basic, bearer.";
                return false;
            }
        }

        if (!TryReadPositiveInt("ACJR3_TIMEOUT_SECONDS", 100, out var timeout, out error))
        {
            return false;
        }

        if (!TryReadNonNegativeInt("ACJR3_MAX_RETRIES", 5, out var maxRetries, out error))
        {
            return false;
        }

        if (!TryReadPositiveInt("ACJR3_RETRY_BASE_DELAY_MS", 500, out var baseDelay, out error))
        {
            return false;
        }

        config = new Acjr3Config(
            siteUrl,
            authMode,
            source.Get("ACJR3_EMAIL"),
            source.Get("ACJR3_API_TOKEN"),
            source.Get("ACJR3_BEARER_TOKEN"),
            timeout,
            maxRetries,
            baseDelay);

        return true;
    }

    private bool TryReadPositiveInt(string name, int fallback, out int value, out string error)
    {
        return TryReadInt(name, fallback, mustBePositive: true, out value, out error);
    }

    private bool TryReadNonNegativeInt(string name, int fallback, out int value, out string error)
    {
        return TryReadInt(name, fallback, mustBePositive: false, out value, out error);
    }

    private bool TryReadInt(string name, int fallback, bool mustBePositive, out int value, out string error)
    {
        value = fallback;
        error = string.Empty;

        var raw = source.Get(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!int.TryParse(raw, out var parsed))
        {
            error = $"{name} must be an integer.";
            return false;
        }

        var valid = mustBePositive ? parsed > 0 : parsed >= 0;
        if (!valid)
        {
            error = mustBePositive
                ? $"{name} must be greater than zero."
                : $"{name} must be zero or greater.";
            return false;
        }

        value = parsed;
        return true;
    }
}
