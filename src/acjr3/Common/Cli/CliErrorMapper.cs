using System.Net;

namespace Acjr3.Common;

public static class CliErrorMapper
{
    public static (CliExitCode ExitCode, string ErrorCode) FromHttpStatus(HttpStatusCode statusCode)
    {
        return (int)statusCode switch
        {
            400 => (CliExitCode.Validation, CliErrorCode.Validation),
            401 => (CliExitCode.Authentication, CliErrorCode.Authentication),
            403 => (CliExitCode.Authentication, CliErrorCode.Authorization),
            404 => (CliExitCode.NotFound, CliErrorCode.NotFound),
            409 => (CliExitCode.Conflict, CliErrorCode.Conflict),
            422 => (CliExitCode.Conflict, CliErrorCode.Conflict),
            408 => (CliExitCode.Network, CliErrorCode.Timeout),
            429 => (CliExitCode.Network, CliErrorCode.Network),
            >= 500 => (CliExitCode.Internal, CliErrorCode.Upstream),
            _ => (CliExitCode.Validation, CliErrorCode.Validation)
        };
    }

    public static (CliExitCode ExitCode, string ErrorCode) FromException(Exception ex)
    {
        if (ex is TaskCanceledException)
        {
            return (CliExitCode.Network, CliErrorCode.Timeout);
        }

        if (ex is HttpRequestException)
        {
            return (CliExitCode.Network, CliErrorCode.Network);
        }

        return (CliExitCode.Internal, CliErrorCode.Internal);
    }
}
