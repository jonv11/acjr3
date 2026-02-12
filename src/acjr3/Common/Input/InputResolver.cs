using System.Text;

namespace Acjr3.Common;

public static class InputResolver
{
    public static bool TryResolveExplicitPayloadSource(
        string? inPath,
        string? body,
        string? bodyFile,
        out ExplicitPayloadSource source,
        out string error)
    {
        source = ExplicitPayloadSource.None;
        error = string.Empty;

        var hasIn = !string.IsNullOrWhiteSpace(inPath);
        var hasBody = !string.IsNullOrWhiteSpace(body);
        var hasBodyFile = !string.IsNullOrWhiteSpace(bodyFile);

        var specified = (hasIn ? 1 : 0) + (hasBody ? 1 : 0) + (hasBodyFile ? 1 : 0);
        if (specified > 1)
        {
            error = "Use exactly one explicit payload source: --in, --body, or --body-file.";
            return false;
        }

        if (hasIn)
        {
            source = ExplicitPayloadSource.In;
        }
        else if (hasBody)
        {
            source = ExplicitPayloadSource.Body;
        }
        else if (hasBodyFile)
        {
            source = ExplicitPayloadSource.BodyFile;
        }

        return true;
    }

    public static bool TryParseFormat(string? raw, out InputFormat format, out string error)
    {
        error = string.Empty;
        format = InputFormat.Json;

        var value = string.IsNullOrWhiteSpace(raw) ? "json" : raw.Trim();
        if (value.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            format = InputFormat.Json;
            return true;
        }

        if (value.Equals("adf", StringComparison.OrdinalIgnoreCase))
        {
            format = InputFormat.Adf;
            return true;
        }

        if (value.Equals("md", StringComparison.OrdinalIgnoreCase)
            || value.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            format = InputFormat.Markdown;
            return true;
        }

        if (value.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            format = InputFormat.Text;
            return true;
        }

        error = "--input-format must be one of: json, adf, md, text.";
        return false;
    }

    public static async Task<(bool Ok, string? Payload, string Error)> TryReadBodyPayloadAsync(
        string? body,
        string? bodyFile,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            return (true, body, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(bodyFile))
        {
            return (true, null, string.Empty);
        }

        try
        {
            var textFromFile = await TextFileInput.ReadAllTextNormalizedAsync(bodyFile, cancellationToken);
            return (true, textFromFile, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to read body file '{bodyFile}': {ex.Message}");
        }
    }

    public static string ContentTypeFor(InputFormat format)
    {
        return format switch
        {
            InputFormat.Json => "application/json",
            InputFormat.Adf => "application/json",
            InputFormat.Markdown => "text/markdown",
            _ => "text/plain"
        };
    }

    public static async Task<(bool Ok, string? Payload, string Error)> TryReadPayloadAsync(
        string? inputPath,
        InputFormat inputFormat,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return (true, null, string.Empty);
        }

        try
        {
            if (inputPath == "-")
            {
                var text = await Console.In.ReadToEndAsync();
                return (true, StripLeadingBom(text), string.Empty);
            }

            var textFromFile = await TextFileInput.ReadAllTextNormalizedAsync(inputPath, cancellationToken);
            return (true, textFromFile, string.Empty);
        }
        catch (Exception ex)
        {
            var source = inputPath == "-" ? "stdin" : $"input file '{inputPath}'";
            return (false, null, $"Failed to read {source}: {ex.Message}");
        }
    }

    private static string StripLeadingBom(string value)
    {
        return value.Length > 0 && value[0] == '\uFEFF' ? value[1..] : value;
    }
}
