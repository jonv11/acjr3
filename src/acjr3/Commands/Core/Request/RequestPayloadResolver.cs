using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Acjr3.Common;

namespace Acjr3.Commands.Core;

internal static class RequestPayloadResolver
{
    public static async Task<(bool Ok, string? Payload, bool PayloadFromDefaultJson, InputFormat InputFormat, ExplicitPayloadSource PayloadSource, string? Error)> ResolveAsync(
        ParseResult parseResult,
        RequestCommandSymbols symbols,
        HttpMethod httpMethod,
        CancellationToken cancellationToken)
    {
        if (!InputResolver.TryParseFormat(parseResult.GetValueForOption(symbols.InputFormatOpt), out var inputFormat, out var formatError))
        {
            return (false, null, false, default, default, formatError);
        }

        if (!InputResolver.TryResolveExplicitPayloadSource(
                parseResult.GetValueForOption(symbols.InOpt),
                parseResult.GetValueForOption(symbols.BodyOpt),
                parseResult.GetValueForOption(symbols.BodyFileOpt),
                out var payloadSource,
                out var payloadSourceError))
        {
            return (false, null, false, default, default, payloadSourceError);
        }

        string? payloadValue = null;
        var payloadFromDefaultJson = false;
        switch (payloadSource)
        {
            case ExplicitPayloadSource.In:
            {
                var payloadLoad = await InputResolver.TryReadPayloadAsync(parseResult.GetValueForOption(symbols.InOpt), inputFormat, cancellationToken);
                if (!payloadLoad.Ok)
                {
                    return (false, null, false, default, default, payloadLoad.Error);
                }

                payloadValue = payloadLoad.Payload;
                break;
            }
            case ExplicitPayloadSource.Body:
            case ExplicitPayloadSource.BodyFile:
            {
                var bodyLoad = await InputResolver.TryReadBodyPayloadAsync(
                    parseResult.GetValueForOption(symbols.BodyOpt),
                    parseResult.GetValueForOption(symbols.BodyFileOpt),
                    cancellationToken);
                if (!bodyLoad.Ok)
                {
                    return (false, null, false, default, default, bodyLoad.Error);
                }

                var optionName = payloadSource == ExplicitPayloadSource.Body ? "--body" : "--body-file";
                if (!JsonPayloadPipeline.TryParseJsonObject(bodyLoad.Payload!, optionName, out var parsedBody, out var parseBodyError))
                {
                    return (false, null, false, default, default, parseBodyError);
                }

                payloadValue = JsonPayloadPipeline.Serialize(parsedBody!);
                break;
            }
            default:
                break;
        }

        if (string.IsNullOrWhiteSpace(payloadValue) && IsMutatingMethod(httpMethod))
        {
            payloadValue = "{}";
            payloadFromDefaultJson = true;
        }

        return (true, payloadValue, payloadFromDefaultJson, inputFormat, payloadSource, null);
    }

    private static bool IsMutatingMethod(HttpMethod method)
    {
        return method == HttpMethod.Post
            || method == HttpMethod.Put
            || method.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
    }
}
