namespace Acjr3.Common;

public static class InputResolver
{
    public static bool TryParseFormat(string? raw, string optionName, out InputFormat format, out string error)
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

        error = $"{optionName} must be one of: json, adf.";
        return false;
    }

    public static async Task<(bool Ok, string? Payload, string Error)> TryReadPayloadAsync(
        string? inputPath,
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
