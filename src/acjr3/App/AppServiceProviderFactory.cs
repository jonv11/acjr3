using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acjr3.App;

public static class AppServiceProviderFactory
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter("Acjr3", LogLevel.Debug);
        });
        services.AddHttpClient("acjr3");
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<RetryPolicy>();
        services.AddSingleton<AuthHeaderProvider>();
        services.AddSingleton<OutputRenderer>();
        services.AddSingleton<RequestExecutor>();
        services.AddSingleton<OpenApiService>();

        var provider = services.BuildServiceProvider();
        ConsoleLogger.SetLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
        return provider;
    }
}
