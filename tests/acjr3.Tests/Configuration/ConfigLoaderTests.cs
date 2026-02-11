using Acjr3;

namespace Acjr3.Tests.Configuration;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void TryLoad_Fails_WhenSiteMissing()
    {
        var source = new DictionaryEnvironmentSource(new Dictionary<string, string?>());
        var loader = new ConfigLoader(source);

        var ok = loader.TryLoad(out _, out var error);

        Assert.False(ok);
        Assert.Contains("ACJR3_SITE_URL", error);
    }

    [Fact]
    public void TryLoad_ParsesDefaults_AndBasicMode()
    {
        var source = new DictionaryEnvironmentSource(new Dictionary<string, string?>
        {
            ["ACJR3_SITE_URL"] = "https://example.atlassian.net/",
            ["ACJR3_EMAIL"] = "user@example.com",
            ["ACJR3_API_TOKEN"] = "token"
        });

        var loader = new ConfigLoader(source);
        var ok = loader.TryLoad(out var config, out var error);

        Assert.True(ok, error);
        Assert.NotNull(config);
        Assert.Equal("https://example.atlassian.net/", config!.SiteUrl.ToString());
        Assert.Equal(AuthMode.Basic, config.AuthMode);
        Assert.Equal(100, config.TimeoutSeconds);
        Assert.Equal(5, config.MaxRetries);
        Assert.Equal(500, config.RetryBaseDelayMs);
    }

    [Fact]
    public void ValidateAuth_Fails_WhenBasicCredsMissing()
    {
        var config = new Acjr3Config(
            new Uri("https://example.atlassian.net"),
            AuthMode.Basic,
            Email: null,
            ApiToken: null,
            BearerToken: null,
            TimeoutSeconds: 100,
            MaxRetries: 5,
            RetryBaseDelayMs: 500);

        var ok = ConfigValidator.TryValidateAuth(config, out var error);

        Assert.False(ok);
        Assert.Contains("ACJR3_EMAIL", error);
    }

    [Fact]
    public void ValidateAuth_Succeeds_ForBearer()
    {
        var config = new Acjr3Config(
            new Uri("https://example.atlassian.net"),
            AuthMode.Bearer,
            Email: null,
            ApiToken: null,
            BearerToken: "abc",
            TimeoutSeconds: 100,
            MaxRetries: 5,
            RetryBaseDelayMs: 500);

        var ok = ConfigValidator.TryValidateAuth(config, out var error);

        Assert.True(ok, error);
    }
}

public sealed class DictionaryEnvironmentSource(Dictionary<string, string?> values) : IEnvironmentSource
{
    public string? Get(string name) => values.TryGetValue(name, out var value) ? value : null;
}


