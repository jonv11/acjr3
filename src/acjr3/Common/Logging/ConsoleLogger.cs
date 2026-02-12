using Microsoft.Extensions.Logging;

namespace Acjr3.Common;

public sealed class ConsoleLogger(bool isVerbose) : IAppLogger
{
    private readonly ILogger logger = CreateLogger();

    private static ILoggerFactory? loggerFactory;

    public bool IsVerbose { get; } = isVerbose;

    public static void SetLoggerFactory(ILoggerFactory factory)
    {
        loggerFactory = factory;
    }

    public void Verbose(string message)
    {
        if (IsVerbose)
        {
            logger.LogDebug("{VerboseMessage}", message);
        }
    }

    private static ILogger CreateLogger()
    {
        if (loggerFactory != null)
        {
            return loggerFactory.CreateLogger("Acjr3.Cli");
        }

        // Fallback for unit tests or execution paths where DI has not been initialized.
        return LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                });
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddFilter("Acjr3", LogLevel.Debug);
            })
            .CreateLogger("Acjr3.Cli");
    }
}
