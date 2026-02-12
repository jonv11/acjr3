namespace Acjr3.Configuration;

public interface IEnvironmentSource
{
    string? Get(string name);
}
