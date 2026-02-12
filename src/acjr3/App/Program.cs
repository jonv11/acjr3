using System.CommandLine;

namespace Acjr3.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        RuntimeOverrideOptions.ApplyProcessConfigOverrides(args);

        var provider = AppServiceProviderFactory.Build();
        var rootCommand = RootCommandFactory.Build(provider);
        return await rootCommand.InvokeAsync(args);
    }

    // TODO: Phase 2 decoupling can remove this shim once Jira command classes
    // stop calling Program.TryLoadValidatedConfig directly.
    internal static bool TryLoadValidatedConfig(bool requireAuth, IAppLogger logger, out Acjr3Config? config, out string error)
    {
        return RuntimeConfigLoader.TryLoadValidatedConfig(requireAuth, logger, out config, out error);
    }
}
