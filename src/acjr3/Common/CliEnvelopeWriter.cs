using System.CommandLine.Invocation;
using Acjr3.Output;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Common;

public static class CliEnvelopeWriter
{
    public static void Write(
        InvocationContext context,
        IServiceProvider? services,
        OutputPreferences output,
        bool success,
        object? data,
        CliError? error,
        int exitCode)
    {
        var renderer = services?.GetService<OutputRenderer>() ?? new OutputRenderer();
        var envelope = new CliEnvelope(
            Success: success,
            Data: data,
            Error: error,
            Meta: new CliMeta("1.0", null, null, null, null, null));
        var text = output.Format == OutputFormat.Text
            ? renderer.RenderText(envelope, output)
            : renderer.RenderEnvelope(envelope, output);
        Console.Out.WriteLine(text);
        context.ExitCode = exitCode;
    }
}
