using System.Net;

namespace Acjr3.Tests.Common;

public sealed class CliErrorMapperTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CliExitCode.Validation, CliErrorCode.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, CliExitCode.Authentication, CliErrorCode.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, CliExitCode.Authentication, CliErrorCode.Authorization)]
    [InlineData(HttpStatusCode.NotFound, CliExitCode.NotFound, CliErrorCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict, CliExitCode.Conflict, CliErrorCode.Conflict)]
    [InlineData((HttpStatusCode)422, CliExitCode.Conflict, CliErrorCode.Conflict)]
    [InlineData(HttpStatusCode.RequestTimeout, CliExitCode.Network, CliErrorCode.Timeout)]
    [InlineData((HttpStatusCode)429, CliExitCode.Network, CliErrorCode.Network)]
    [InlineData(HttpStatusCode.InternalServerError, CliExitCode.Internal, CliErrorCode.Upstream)]
    [InlineData(HttpStatusCode.BadGateway, CliExitCode.Internal, CliErrorCode.Upstream)]
    [InlineData(HttpStatusCode.Created, CliExitCode.Validation, CliErrorCode.Validation)]
    public void FromHttpStatus_MapsExpectedCodes(HttpStatusCode statusCode, CliExitCode expectedExit, string expectedCode)
    {
        var (exitCode, errorCode) = CliErrorMapper.FromHttpStatus(statusCode);

        Assert.Equal(expectedExit, exitCode);
        Assert.Equal(expectedCode, errorCode);
    }

    [Fact]
    public void FromException_TaskCanceled_MapsTimeout()
    {
        var (exitCode, errorCode) = CliErrorMapper.FromException(new TaskCanceledException("timed out"));

        Assert.Equal(CliExitCode.Network, exitCode);
        Assert.Equal(CliErrorCode.Timeout, errorCode);
    }

    [Fact]
    public void FromException_HttpRequest_MapsNetwork()
    {
        var (exitCode, errorCode) = CliErrorMapper.FromException(new HttpRequestException("network"));

        Assert.Equal(CliExitCode.Network, exitCode);
        Assert.Equal(CliErrorCode.Network, errorCode);
    }

    [Fact]
    public void FromException_Default_MapsInternal()
    {
        var (exitCode, errorCode) = CliErrorMapper.FromException(new InvalidOperationException("boom"));

        Assert.Equal(CliExitCode.Internal, exitCode);
        Assert.Equal(CliErrorCode.Internal, errorCode);
    }
}
