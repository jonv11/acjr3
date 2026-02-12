using System.Text;

namespace Acjr3.Common;

public enum ExplicitPayloadSource
{
    None,
    In,
    Body,
    BodyFile
}

public enum InputFormat
{
    Json,
    Adf,
    Markdown,
    Text
}

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

public sealed record StoredRequest(
    string Method,
    string Path,
    IReadOnlyList<KeyValuePair<string, string>> Query,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string Accept,
    string? ContentType,
    string? Body);

public static class RequestRecording
{
    public static async Task SaveAsync(string path, StoredRequest request, CancellationToken cancellationToken)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            request,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }

    public static async Task<(bool Ok, StoredRequest? Request, string Error)> LoadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await TextFileInput.ReadAllTextNormalizedAsync(path, cancellationToken);
            var request = System.Text.Json.JsonSerializer.Deserialize<StoredRequest>(text);
            if (request is null
                || string.IsNullOrWhiteSpace(request.Method)
                || string.IsNullOrWhiteSpace(request.Path))
            {
                return (false, null, $"Replay file '{path}' does not contain a valid request.");
            }

            return (true, request, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to read replay file '{path}': {ex.Message}");
        }
    }
}
