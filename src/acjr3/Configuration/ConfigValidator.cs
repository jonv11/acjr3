namespace Acjr3.Configuration;

public static class ConfigValidator
{
    public static bool TryValidateAuth(Acjr3Config config, out string error)
    {
        error = string.Empty;

        if (config.AuthMode == AuthMode.Basic)
        {
            if (string.IsNullOrWhiteSpace(config.Email))
            {
                error = "ACJR3_EMAIL is required for basic auth.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.ApiToken))
            {
                error = "ACJR3_API_TOKEN is required for basic auth.";
                return false;
            }

            return true;
        }

        if (string.IsNullOrWhiteSpace(config.BearerToken))
        {
            error = "ACJR3_BEARER_TOKEN is required for bearer auth.";
            return false;
        }

        return true;
    }
}
