using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Acjr3.Common;

namespace Acjr3.Commands.Core;

internal static class RequestPayloadResolver
{
    public static async Task<(bool Ok, string? Payload, string? Error)> ResolveAsync(
        ParseResult parseResult,
        RequestCommandSymbols symbols,
        HttpMethod httpMethod,
        CancellationToken cancellationToken)
    {
        string? payloadValue = null;
        var inPath = parseResult.GetValueForOption(symbols.InOpt);
        if (!string.IsNullOrWhiteSpace(inPath))
        {
            var payloadLoad = await InputResolver.TryReadPayloadAsync(inPath, cancellationToken);
            if (!payloadLoad.Ok)
            {
                return (false, null, payloadLoad.Error);
            }

            if (string.IsNullOrWhiteSpace(payloadLoad.Payload))
            {
                return (false, null, "--in was provided but no payload was read.");
            }

            if (!JsonPayloadPipeline.TryParseJsonObject(payloadLoad.Payload, "--in", out var parsedInPayload, out var parseInError))
            {
                return (false, null, parseInError);
            }

            payloadValue = JsonPayloadPipeline.Serialize(parsedInPayload!);
        }

        if (string.IsNullOrWhiteSpace(payloadValue) && IsMutatingMethod(httpMethod))
        {
            payloadValue = "{}";
        }

        return (true, payloadValue, null);
    }

    private static bool IsMutatingMethod(HttpMethod method)
    {
        return method == HttpMethod.Post
            || method == HttpMethod.Put
            || method.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
    }
}
