using System.CommandLine.Parsing;
using System.Linq;

namespace System.CommandLine
{
    public static class CommandLineCompatibility
    {
        public static void AddCommand(this Command command, Command child) => command.Add(child);

        public static void AddArgument(this Command command, Argument argument) => command.Add(argument);

        public static void AddOption(this Command command, Option option) => command.Add(option);

        public static void AddGlobalOption(this Command command, Option option)
        {
            option.Recursive = true;
            command.Add(option);
        }

        public static void SetHandler(this Command command, Action<Invocation.InvocationContext> action)
        {
            command.SetAction(parseResult =>
            {
                var context = new Invocation.InvocationContext(parseResult, CancellationToken.None);
                action(context);
                return context.ExitCode;
            });
        }

        public static void SetHandler(this Command command, Func<Invocation.InvocationContext, Task> action)
        {
            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var context = new Invocation.InvocationContext(parseResult, cancellationToken);
                await action(context);
                return context.ExitCode;
            });
        }

        public static T? GetValueForOption<T>(this ParseResult parseResult, Option<T> option) => parseResult.GetValue(option);

        public static T? GetValueForArgument<T>(this ParseResult parseResult, Argument<T> argument) => parseResult.GetValue(argument);

        public static OptionResult? FindResultFor<T>(this ParseResult parseResult, Option<T> option) => parseResult.GetResult(option);

        public static bool HasAlias(this Option option, string alias) => option.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase);

        public static Task<int> InvokeAsync(this RootCommand rootCommand, string[] args) => rootCommand.Parse(args).InvokeAsync();
    }
}

namespace System.CommandLine.Invocation
{
    public sealed class InvocationContext
    {
        public InvocationContext(ParseResult parseResult, CancellationToken cancellationToken)
        {
            ParseResult = parseResult;
            cancellationTokenValue = cancellationToken;
        }

        private readonly CancellationToken cancellationTokenValue;

        public ParseResult ParseResult { get; }

        public int ExitCode { get; set; }

        public CancellationToken GetCancellationToken() => cancellationTokenValue;
    }
}
