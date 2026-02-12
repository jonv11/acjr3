using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Acjr3.App;

namespace Acjr3.Commands.Jira;

internal static class JiraCommandPreflight
{
    public static bool TryPrepare(
        InvocationContext context,
        Option<bool> verboseOption,
        out ParseResult parseResult,
        out IAppLogger logger,
        out Acjr3Config? config,
        out OutputPreferences outputPreferences)
    {
        parseResult = context.ParseResult;
        logger = new ConsoleLogger(parseResult.GetValueForOption(verboseOption));
        config = null;
        outputPreferences = OutputPreferences.Default;

        if (!RuntimeConfigLoader.TryLoadValidatedConfig(requireAuth: true, logger, out config, out var configError))
        {
            CliOutput.WriteValidationError(context, configError);
            return false;
        }

        if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out outputPreferences))
        {
            return false;
        }

        return true;
    }
}
