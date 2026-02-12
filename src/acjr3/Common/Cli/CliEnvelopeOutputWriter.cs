using System.CommandLine.Invocation;
using Acjr3.Output;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Common;

internal static class CliEnvelopeOutputWriter
{
    public static void Write(InvocationContext context, IServiceProvider? services, OutputPreferences output, CliEnvelope envelope, int exitCode)
    {
        var renderer = services?.GetService<OutputRenderer>() ?? new OutputRenderer();
        var text = output.Format == OutputFormat.Text
            ? renderer.RenderText(envelope, output)
            : renderer.RenderEnvelope(envelope, output);
        Console.Out.WriteLine(text);
        context.ExitCode = exitCode;
    }
}
