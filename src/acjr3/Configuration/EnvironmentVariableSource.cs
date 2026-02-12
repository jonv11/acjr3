namespace Acjr3.Configuration;

public sealed class EnvironmentVariableSource : IEnvironmentSource
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
