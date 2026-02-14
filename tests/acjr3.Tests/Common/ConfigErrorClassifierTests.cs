namespace Acjr3.Tests.Common;

public sealed class ConfigErrorClassifierTests
{
    [Fact]
    public void Classify_BasicAuthRequired_MapsAuthentication()
    {
        var (exitCode, errorCode) = ConfigErrorClassifier.Classify("Email is required for BASIC auth.");

        Assert.Equal(CliExitCode.Authentication, exitCode);
        Assert.Equal(CliErrorCode.Authentication, errorCode);
    }

    [Fact]
    public void Classify_BearerAuthRequired_MapsAuthentication()
    {
        var (exitCode, errorCode) = ConfigErrorClassifier.Classify("Bearer token is required for bearer auth.");

        Assert.Equal(CliExitCode.Authentication, exitCode);
        Assert.Equal(CliErrorCode.Authentication, errorCode);
    }

    [Fact]
    public void Classify_CaseInsensitiveMatch_MapsAuthentication()
    {
        var (exitCode, errorCode) = ConfigErrorClassifier.Classify("API TOKEN IS REQUIRED FOR BASIC AUTH.");

        Assert.Equal(CliExitCode.Authentication, exitCode);
        Assert.Equal(CliErrorCode.Authentication, errorCode);
    }

    [Fact]
    public void Classify_OtherMessage_MapsValidation()
    {
        var (exitCode, errorCode) = ConfigErrorClassifier.Classify("Unknown configuration problem.");

        Assert.Equal(CliExitCode.Validation, exitCode);
        Assert.Equal(CliErrorCode.Validation, errorCode);
    }
}
