using System.CommandLine;
using System.CommandLine.Invocation;
using Acjr3.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Core;

public static class OpenApiCommandBuilder
{
    public static Command Build(IServiceProvider services)
    {
        var openapi = new Command("openapi", "OpenAPI discovery commands");

        var fetch = new Command("fetch", "Fetch Jira Cloud REST OpenAPI spec to cache or output file");
        var fetchOut = new Option<string?>("--out") { Description = "Output file path. Defaults to local cache path." };
        var fetchSpecUrl = new Option<string?>("--spec-url") { Description = "Optional explicit OpenAPI URL." };
        fetch.AddOption(fetchOut);
        fetch.AddOption(fetchSpecUrl);
        fetch.SetHandler(async (InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var outPath = context.ParseResult.GetValueForOption(fetchOut);
            var specUrl = context.ParseResult.GetValueForOption(fetchSpecUrl);
            var service = services.GetRequiredService<OpenApiService>();
            var logger = new ConsoleLogger(false);
            var result = await service.FetchAsync(outPath, specUrl, logger);
            if (!result.Success)
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, result.Message, null, null), (int)CliExitCode.Validation);
                return;
            }

            CliEnvelopeWriter.Write(context, null, output, true, new { message = result.Message }, null, (int)CliExitCode.Success);
        });

        var paths = new Command("paths", "List paths and methods from cached or provided OpenAPI spec");
        var pathFilterOpt = new Option<string?>("--path-filter") { Description = "Filter text for path/method/operationId." };
        var specFileOpt = new Option<string?>("--spec-file") { Description = "Use local OpenAPI spec file instead of cache" };
        paths.AddOption(pathFilterOpt);
        paths.AddOption(specFileOpt);
        paths.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var filter = context.ParseResult.GetValueForOption(pathFilterOpt);
            var specFile = context.ParseResult.GetValueForOption(specFileOpt);
            var service = services.GetRequiredService<OpenApiService>();
            var result = service.ListPaths(filter, specFile);
            if (!result.Success)
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, result.Message, null, null), (int)CliExitCode.Validation);
                return;
            }

            CliEnvelopeWriter.Write(context, null, output, true, new { message = result.Message, lines = result.Lines }, null, (int)CliExitCode.Success);
        });

        var show = new Command("show", "Show method/path details from OpenAPI spec");
        var methodArg = new Argument<string>("method") { Description = "HTTP method" };
        var pathArg = new Argument<string>("path") { Description = "OpenAPI path" };
        var showSpecFileOpt = new Option<string?>("--spec-file") { Description = "Use local OpenAPI spec file instead of cache" };
        show.AddArgument(methodArg);
        show.AddArgument(pathArg);
        show.AddOption(showSpecFileOpt);
        show.SetHandler((InvocationContext context) =>
        {
            if (!OutputOptionBinding.TryResolveOrReport(context.ParseResult, context, out var output))
            {
                return;
            }

            var method = context.ParseResult.GetValueForArgument(methodArg);
            var path = context.ParseResult.GetValueForArgument(pathArg);
            var specFile = context.ParseResult.GetValueForOption(showSpecFileOpt);
            var service = services.GetRequiredService<OpenApiService>();
            var result = service.ShowOperation(method, path, specFile);
            if (!result.Success)
            {
                CliEnvelopeWriter.Write(context, null, output, false, null, new CliError(CliErrorCode.Validation, result.Message, null, null), (int)CliExitCode.Validation);
                return;
            }

            CliEnvelopeWriter.Write(context, null, output, true, new { message = result.Message, lines = result.Lines }, null, (int)CliExitCode.Success);
        });

        openapi.AddCommand(fetch);
        openapi.AddCommand(paths);
        openapi.AddCommand(show);

        return openapi;
    }
}

