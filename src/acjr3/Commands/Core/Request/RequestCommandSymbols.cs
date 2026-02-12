using System.CommandLine;

namespace Acjr3.Commands.Core;

internal sealed record RequestCommandSymbols(
    Argument<string?> MethodArg,
    Argument<string?> PathArg,
    Option<string[]> QueryOpt,
    Option<string[]> HeaderOpt,
    Option<string> AcceptOpt,
    Option<string?> ContentTypeOpt,
    Option<string?> BodyOpt,
    Option<string?> BodyFileOpt,
    Option<string?> InOpt,
    Option<string> InputFormatOpt,
    Option<string?> OutOpt,
    Option<bool> FailOnNonSuccessOpt,
    Option<bool> VerboseOpt,
    Option<bool> DebugOpt,
    Option<bool> TraceOpt,
    Option<bool> RetryNonIdempotentOpt,
    Option<bool> PaginateOpt,
    Option<bool> ExplainOpt,
    Option<string?> RequestFileOpt,
    Option<string?> ReplayOpt,
    Option<bool> YesOpt,
    Option<bool> ForceOpt);
