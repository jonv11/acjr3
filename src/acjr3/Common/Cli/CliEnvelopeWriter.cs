using System.CommandLine.Invocation;
using Acjr3.Output;

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
        var envelope = new CliEnvelope(
            Success: success,
            Data: data,
            Error: error,
            Meta: new CliMeta("1.0", null, null, null, null, null));
        CliEnvelopeOutputWriter.Write(context, services, output, envelope, exitCode);
    }
}
