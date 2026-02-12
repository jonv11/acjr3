using System.Text;
using System.Text.Json;

namespace Acjr3.Output;

public sealed class OutputRenderer
{
    private readonly JsonSerializerOptions prettyJsonOptions = new() { WriteIndented = true };
    private readonly JsonSerializerOptions compactJsonOptions = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions compactOptions = new() { WriteIndented = false };

    public string RenderEnvelope(CliEnvelope envelope, OutputPreferences preferences)
    {
        var transformedData = OutputDataTransformer.TransformData(envelope.Data, preferences);
        var transformedEnvelope = envelope with { Data = transformedData };
        var options = preferences.JsonStyle == JsonStyle.Pretty ? prettyJsonOptions : compactJsonOptions;

        if (preferences.Format == OutputFormat.Jsonl)
        {
            return RenderJsonLines(transformedEnvelope, options);
        }

        return JsonSerializer.Serialize(transformedEnvelope, options);
    }

    public string RenderText(CliEnvelope envelope, OutputPreferences preferences)
    {
        if (!envelope.Success)
        {
            return envelope.Error?.Message ?? "Unknown error";
        }

        var transformedData = OutputDataTransformer.TransformData(envelope.Data, preferences);
        if (transformedData is null)
        {
            return string.Empty;
        }

        if (preferences.Plain)
        {
            return OutputDataTransformer.ExtractScalarText(transformedData);
        }

        if (transformedData is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            var options = preferences.JsonStyle == JsonStyle.Pretty ? prettyJsonOptions : compactJsonOptions;
            return JsonSerializer.Serialize(element, options);
        }

        if (transformedData is string text)
        {
            return text;
        }

        return JsonSerializer.Serialize(transformedData, prettyJsonOptions);
    }

    private static string RenderJsonLines(CliEnvelope envelope, JsonSerializerOptions jsonOptions)
    {
        if (envelope.Data is not JsonElement json || json.ValueKind != JsonValueKind.Array)
        {
            return JsonSerializer.Serialize(envelope, jsonOptions);
        }

        var builder = new StringBuilder();
        foreach (var item in json.EnumerateArray())
        {
            var lineEnvelope = envelope with { Data = item.Clone() };
            builder.AppendLine(JsonSerializer.Serialize(lineEnvelope, compactOptions));
        }

        return builder.ToString().TrimEnd();
    }
}
