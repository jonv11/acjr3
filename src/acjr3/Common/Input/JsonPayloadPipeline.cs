using System.Text.Json;
using System.Text.Json.Nodes;

namespace Acjr3.Common;

public static class JsonPayloadPipeline
{
    public static bool TryParseJsonObject(string payload, string optionName, out JsonObject? jsonObject, out string error)
    {
        jsonObject = null;
        error = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = $"{optionName} payload must be a JSON object.";
                return false;
            }

            jsonObject = JsonNode.Parse(payload)?.AsObject();
            if (jsonObject is null)
            {
                error = $"{optionName} payload must be a JSON object.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to parse {optionName} payload: {ex.Message}";
            return false;
        }
    }

    public static bool TryReadJsonObjectFile(string filePath, string optionName, out JsonObject? jsonObject, out string error)
    {
        jsonObject = null;
        error = string.Empty;

        try
        {
            var text = TextFileInput.ReadAllTextNormalized(filePath);
            return TryParseJsonObject(text, optionName, out jsonObject, out error);
        }
        catch (Exception ex)
        {
            error = $"Failed to read {optionName} file '{filePath}': {ex.Message}";
            return false;
        }
    }

    public static JsonObject ParseDefaultPayload(string payload)
    {
        var jsonObject = JsonNode.Parse(payload)?.AsObject();
        if (jsonObject is null)
        {
            throw new InvalidOperationException("Default JSON payload must be a JSON object.");
        }

        return jsonObject;
    }

    public static JsonObject EnsureObjectPath(JsonObject root, params string[] path)
    {
        JsonObject current = root;
        foreach (var segment in path)
        {
            if (current[segment] is JsonObject existing)
            {
                current = existing;
                continue;
            }

            var next = new JsonObject();
            current[segment] = next;
            current = next;
        }

        return current;
    }

    public static void SetNode(JsonObject root, JsonNode? value, params string[] path)
    {
        if (path.Length == 0)
        {
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        }

        if (path.Length == 1)
        {
            root[path[0]] = value;
            return;
        }

        var parentPath = path[..^1];
        var parent = EnsureObjectPath(root, parentPath);
        parent[path[^1]] = value;
    }

    public static void SetString(JsonObject root, string value, params string[] path)
    {
        SetNode(root, JsonValue.Create(value), path);
    }

    public static string? TryGetString(JsonObject root, params string[] path)
    {
        var node = TryGetNode(root, path);
        return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    public static JsonNode? TryGetNode(JsonObject root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject currentObject)
            {
                return null;
            }

            if (!currentObject.TryGetPropertyValue(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    public static bool HasMeaningfulNode(JsonNode? node)
    {
        if (node is null)
        {
            return false;
        }

        if (node is JsonObject obj)
        {
            return obj.Count > 0;
        }

        if (node is JsonArray array)
        {
            return array.Count > 0;
        }

        return true;
    }

    public static string Serialize(JsonObject payload)
    {
        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
