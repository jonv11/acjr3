using System.Text.Json;

namespace Acjr3.Output;

internal static class OutputDataTransformer
{
    public static object? TransformData(object? data, OutputPreferences preferences)
    {
        if (data is not JsonElement json)
        {
            return data;
        }

        var value = json.Clone();
        if (!string.IsNullOrWhiteSpace(preferences.Filter))
        {
            value = ApplyFilter(value, preferences.Filter!);
        }

        if (!string.IsNullOrWhiteSpace(preferences.Sort))
        {
            value = ApplySort(value, preferences.Sort!);
        }

        if (preferences.Limit is { } limit)
        {
            value = ApplyLimit(value, limit);
        }

        if (!string.IsNullOrWhiteSpace(preferences.Select))
        {
            value = ApplySelect(value, preferences.Select!);
        }

        return value;
    }

    public static string ToScalarString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    public static string ExtractScalarText(object data)
    {
        if (data is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var scalars = element.EnumerateArray().Select(ToScalarString);
                return string.Join(Environment.NewLine, scalars);
            }

            return ToScalarString(element);
        }

        return data.ToString() ?? string.Empty;
    }

    private static JsonElement ApplyFilter(JsonElement source, string expression)
    {
        if (source.ValueKind != JsonValueKind.Array)
        {
            return source;
        }

        var idx = expression.IndexOf('=');
        if (idx <= 0 || idx == expression.Length - 1)
        {
            return source;
        }

        var field = expression[..idx].Trim();
        var expected = expression[(idx + 1)..].Trim().Trim('\'', '"');
        var filtered = source
            .EnumerateArray()
            .Where(item => TryGetNestedValue(item, field, out var nested) && string.Equals(ToScalarString(nested), expected, StringComparison.Ordinal))
            .ToArray();

        return JsonSerializer.SerializeToElement(filtered);
    }

    private static JsonElement ApplySort(JsonElement source, string expression)
    {
        if (source.ValueKind != JsonValueKind.Array)
        {
            return source;
        }

        var parts = expression.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return source;
        }

        var field = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
        var items = source.EnumerateArray().ToArray();
        Array.Sort(items, (a, b) =>
        {
            var aValue = TryGetNestedValue(a, field, out var aNested) ? ToScalarString(aNested) : string.Empty;
            var bValue = TryGetNestedValue(b, field, out var bNested) ? ToScalarString(bNested) : string.Empty;
            return string.Compare(aValue, bValue, StringComparison.Ordinal);
        });

        if (descending)
        {
            Array.Reverse(items);
        }

        return JsonSerializer.SerializeToElement(items);
    }

    private static JsonElement ApplyLimit(JsonElement source, int limit)
    {
        if (source.ValueKind != JsonValueKind.Array || limit < 0)
        {
            return source;
        }

        var items = source.EnumerateArray().Take(limit).ToArray();
        return JsonSerializer.SerializeToElement(items);
    }

    private static JsonElement ApplySelect(JsonElement source, string selectorsRaw)
    {
        var selectors = selectorsRaw
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.Length > 0)
            .ToArray();

        if (selectors.Length == 0)
        {
            return source;
        }

        if (source.ValueKind == JsonValueKind.Array)
        {
            var projected = source
                .EnumerateArray()
                .Select(item => ApplySelectToObject(item, selectors))
                .ToArray();
            return JsonSerializer.SerializeToElement(projected);
        }

        if (source.ValueKind == JsonValueKind.Object)
        {
            return ApplySelectToObject(source, selectors);
        }

        return source;
    }

    private static JsonElement ApplySelectToObject(JsonElement source, IReadOnlyList<string> selectors)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return source;
        }

        var projected = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var selector in selectors)
        {
            if (!TryGetNestedValue(source, selector, out var selected))
            {
                continue;
            }

            var key = selector.Contains('.', StringComparison.Ordinal)
                ? selector[(selector.LastIndexOf('.') + 1)..]
                : selector;
            projected[key] = selected.Clone();
        }

        return JsonSerializer.SerializeToElement(projected);
    }

    private static bool TryGetNestedValue(JsonElement source, string path, out JsonElement value)
    {
        value = source;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out var nested))
            {
                value = default;
                return false;
            }

            value = nested;
        }

        return true;
    }
}
