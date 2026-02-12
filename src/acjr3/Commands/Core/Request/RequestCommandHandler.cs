using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Acjr3.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Core;

internal static class RequestCommandHandler
{
    public static async Task HandleAsync(IServiceProvider services, InvocationContext context, RequestCommandSymbols symbols)
    {
        var parseResult = context.ParseResult;
        var logger = new ConsoleLogger(
            parseResult.GetValueForOption(symbols.VerboseOpt)
            || parseResult.GetValueForOption(symbols.DebugOpt)
            || parseResult.GetValueForOption(symbols.TraceOpt));

        if (!OutputOptionBinding.TryResolveOrReport(parseResult, context, out var outputPreferences))
        {
            return;
        }

        if (!RuntimeConfigLoader.TryLoadValidatedConfig(requireAuth: true, logger, out var config, out var configError))
        {
            var (exitCode, errorCode) = ConfigErrorClassifier.Classify(configError);
            CliEnvelopeWriter.Write(context, services, outputPreferences, false, null, new CliError(errorCode, configError, null, null), (int)exitCode);
            return;
        }

        var methodRaw = parseResult.GetValueForArgument(symbols.MethodArg);
        var path = parseResult.GetValueForArgument(symbols.PathArg);
        var replayPath = parseResult.GetValueForOption(symbols.ReplayOpt);
        StoredRequest? replayRequest = null;
        if (!string.IsNullOrWhiteSpace(replayPath))
        {
            var replayLoaded = await RequestRecording.LoadAsync(replayPath!, context.GetCancellationToken());
            if (!replayLoaded.Ok)
            {
                CliEnvelopeWriter.Write(context, services, outputPreferences, false, null, new CliError(CliErrorCode.Validation, replayLoaded.Error, null, null), (int)CliExitCode.Validation);
                return;
            }

            replayRequest = replayLoaded.Request!;
            methodRaw = replayRequest.Method;
            path = replayRequest.Path;
        }

        if (string.IsNullOrWhiteSpace(methodRaw) || string.IsNullOrWhiteSpace(path))
        {
            CliEnvelopeWriter.Write(context, services, outputPreferences, false, null, new CliError(CliErrorCode.Validation, "Provide METHOD and PATH, or use --replay <file>.", null, null), (int)CliExitCode.Validation);
            return;
        }

        if (!HttpMethodParser.TryParse(methodRaw!, out var httpMethod, out var methodError))
        {
            CliEnvelopeWriter.Write(context, services, outputPreferences, false, null, new CliError(CliErrorCode.Validation, methodError, null, null), (int)CliExitCode.Validation);
            return;
        }

        if (!RequestOptionParser.TryParsePairs(parseResult.GetValueForOption(symbols.QueryOpt) ?? [], out var queryPairs, out var queryError))
        {
            CliEnvelopeWriter.Write(context, services, outputPreferences, false, null, new CliError(CliErrorCode.Validation, queryError, null, null), (int)CliExitCode.Validation);
            return;
        }

        if (!RequestOptionParser.TryParsePairs(parseResult.GetValueForOption(symbols.HeaderOpt) ?? [], out var headerPairs, out var headerError))
        {
            CliEnvelopeWriter.Write(context, services, outputPreferences, false, null, new CliError(CliErrorCode.Validation, headerError, null, null), (int)CliExitCode.Validation);
            return;
        }

        var payloadResolved = await RequestPayloadResolver.ResolveAsync(parseResult, symbols, httpMethod!, context.GetCancellationToken());
        if (!payloadResolved.Ok)
        {
            CliEnvelopeWriter.Write(context, services, outputPreferences, false, null, new CliError(CliErrorCode.Validation, payloadResolved.Error!, null, null), (int)CliExitCode.Validation);
            return;
        }

        var payloadValue = payloadResolved.Payload;
        var payloadFromDefaultJson = payloadResolved.PayloadFromDefaultJson;
        var inputFormat = payloadResolved.InputFormat;
        var payloadSource = payloadResolved.PayloadSource;

        if (replayRequest is not null)
        {
            if (queryPairs.Count == 0 && replayRequest.Query.Count > 0)
            {
                queryPairs = replayRequest.Query.ToList();
            }

            if (headerPairs.Count == 0 && replayRequest.Headers.Count > 0)
            {
                headerPairs = replayRequest.Headers.ToList();
            }

            if (string.IsNullOrWhiteSpace(payloadValue) && !string.IsNullOrWhiteSpace(replayRequest.Body))
            {
                payloadValue = replayRequest.Body;
            }
        }

        var acceptValue = parseResult.GetValueForOption(symbols.AcceptOpt)!;
        if (replayRequest is not null && !IsOptionSpecified(parseResult, symbols.AcceptOpt))
        {
            acceptValue = replayRequest.Accept;
        }

        var requestContentType = parseResult.GetValueForOption(symbols.ContentTypeOpt);
        if (replayRequest is not null
            && string.IsNullOrWhiteSpace(requestContentType)
            && !string.IsNullOrWhiteSpace(replayRequest.ContentType))
        {
            requestContentType = replayRequest.ContentType;
        }

        if (!string.IsNullOrWhiteSpace(payloadValue) && string.IsNullOrWhiteSpace(requestContentType))
        {
            if (payloadFromDefaultJson
                || payloadSource == ExplicitPayloadSource.Body
                || payloadSource == ExplicitPayloadSource.BodyFile)
            {
                requestContentType = "application/json";
            }
            else
            {
                requestContentType = InputResolver.ContentTypeFor(inputFormat);
            }
        }

        var storedRequest = new StoredRequest(
            httpMethod!.Method,
            path!,
            queryPairs,
            headerPairs,
            acceptValue,
            requestContentType,
            payloadValue);

        var requestFilePath = parseResult.GetValueForOption(symbols.RequestFileOpt);
        if (!string.IsNullOrWhiteSpace(requestFilePath))
        {
            await RequestRecording.SaveAsync(requestFilePath!, storedRequest, context.GetCancellationToken());
        }

        if (parseResult.GetValueForOption(symbols.ExplainOpt))
        {
            CliEnvelopeWriter.Write(context, services, outputPreferences, true, new
            {
                method = storedRequest.Method,
                path = storedRequest.Path,
                query = storedRequest.Query,
                headers = storedRequest.Headers.Select(h => new { key = h.Key, value = Redactor.RedactHeader(h.Key, h.Value) }),
                contentType = storedRequest.ContentType,
                body = storedRequest.Body,
                requestFile = requestFilePath
            }, null, (int)CliExitCode.Success);
            return;
        }

        var options = new RequestCommandOptions(
            httpMethod,
            path!,
            queryPairs,
            headerPairs,
            acceptValue,
            requestContentType,
            payloadValue,
            parseResult.GetValueForOption(symbols.OutOpt),
            outputPreferences,
            parseResult.GetValueForOption(symbols.FailOnNonSuccessOpt),
            parseResult.GetValueForOption(symbols.RetryNonIdempotentOpt),
            parseResult.GetValueForOption(symbols.PaginateOpt) || outputPreferences.All,
            parseResult.GetValueForOption(symbols.YesOpt) || parseResult.GetValueForOption(symbols.ForceOpt));

        var executor = services.GetRequiredService<RequestExecutor>();
        context.ExitCode = await executor.ExecuteAsync(config!, options, logger, context.GetCancellationToken());
    }

    private static bool IsOptionSpecified<T>(ParseResult parseResult, Option<T> option)
    {
        return parseResult.FindResultFor(option) is OptionResult { Tokens.Count: > 0 };
    }
}
