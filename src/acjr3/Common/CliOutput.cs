using System.CommandLine.Invocation;
using Acjr3.Output;

namespace Acjr3.Common;

public static class CliOutput
{
    private const string EnvelopeVersion = "1.0";
    private static readonly OutputRenderer Renderer = new();

    public static void WriteValidationError(InvocationContext context, string message, object? details = null, string? hint = null)
    {
        if (LooksLikeAuthenticationError(message))
        {
            WriteAuthenticationError(context, message, details, hint);
            return;
        }

        WriteError(context, CliExitCode.Validation, CliErrorCode.Validation, message, details, hint);
    }

    public static void WriteAuthenticationError(InvocationContext context, string message, object? details = null, string? hint = null)
    {
        WriteError(context, CliExitCode.Authentication, CliErrorCode.Authentication, message, details, hint);
    }

    public static void WriteError(
        InvocationContext context,
        CliExitCode exitCode,
        string errorCode,
        string message,
        object? details = null,
        string? hint = null)
    {
        var preferences = OutputPreferences.Default;
        if (OutputOptionBinding.TryResolve(context.ParseResult, out var resolved, out _))
        {
            preferences = resolved;
        }

        var envelope = new CliEnvelope(
            Success: false,
            Data: null,
            Error: new CliError(errorCode, message, details, hint),
            Meta: new CliMeta(EnvelopeVersion, null, null, null, null, null));

        var text = preferences.Format == OutputFormat.Text
            ? Renderer.RenderText(envelope, preferences)
            : Renderer.RenderEnvelope(envelope, preferences);
        Console.Out.WriteLine(text);
        context.ExitCode = (int)exitCode;
    }

    private static bool LooksLikeAuthenticationError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("required for basic auth", StringComparison.OrdinalIgnoreCase)
            || message.Contains("required for bearer auth", StringComparison.OrdinalIgnoreCase)
            || message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || message.Contains("authorization", StringComparison.OrdinalIgnoreCase);
    }
}
