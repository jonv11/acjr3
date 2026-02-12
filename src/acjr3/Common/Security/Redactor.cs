namespace Acjr3.Common;

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
