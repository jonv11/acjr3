using System.Text;
using Acjr3;

namespace Acjr3.Tests.Common;

public sealed class AuthAndRedactionTests
{
    [Fact]
    public void AuthHeaderProvider_CreatesBasicHeader()
    {
        var provider = new AuthHeaderProvider();
        var config = new Acjr3Config(
            new Uri("https://example.atlassian.net"),
            AuthMode.Basic,
            "me@example.com",
            "tok",
            null,
            100,
            5,
            500);

        var header = provider.Create(config);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter!));

        Assert.Equal("Basic", header.Scheme);
        Assert.Equal("me@example.com:tok", decoded);
    }

    [Fact]
    public void Redactor_RedactsAuthorizationHeader()
    {
        var value = Redactor.RedactHeader("Authorization", "Bearer secret");
        Assert.Equal("<redacted>", value);
    }

    [Fact]
    public void Redactor_MasksSecrets()
    {
        var masked = Redactor.MaskSecret("abcdef");
        Assert.Equal("ab***ef", masked);
    }
}


