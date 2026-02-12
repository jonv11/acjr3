namespace Acjr3.Common;

public static class ConfigErrorClassifier
{
    public static (CliExitCode ExitCode, string ErrorCode) Classify(string message)
    {
        if (message.Contains("required for basic auth", StringComparison.OrdinalIgnoreCase)
            || message.Contains("required for bearer auth", StringComparison.OrdinalIgnoreCase))
        {
            return (CliExitCode.Authentication, CliErrorCode.Authentication);
        }

        return (CliExitCode.Validation, CliErrorCode.Validation);
    }
}
