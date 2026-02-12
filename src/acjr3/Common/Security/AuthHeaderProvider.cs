using System.Net.Http.Headers;
using System.Text;

namespace Acjr3.Common;

public sealed class AuthHeaderProvider
{
    public AuthenticationHeaderValue Create(Acjr3Config config)
    {
        return config.AuthMode switch
        {
            AuthMode.Basic => BuildBasic(config.Email!, config.ApiToken!),
            AuthMode.Bearer => new AuthenticationHeaderValue("Bearer", config.BearerToken),
            _ => throw new InvalidOperationException($"Unsupported auth mode {config.AuthMode}")
        };
    }

    private static AuthenticationHeaderValue BuildBasic(string email, string token)
    {
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
        return new AuthenticationHeaderValue("Basic", value);
    }
}
