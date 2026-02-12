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
}
