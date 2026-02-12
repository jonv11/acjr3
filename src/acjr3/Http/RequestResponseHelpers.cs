using System.Net;
using System.Text;
using System.Text.Json;

namespace Acjr3.Http;

internal static class RequestResponseHelpers
{
    public static async Task<object> SaveBodyToFileAsync(HttpResponseMessage response, string outPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        await File.WriteAllBytesAsync(outPath, bytes, cancellationToken);
        return new
        {
            file = outPath,
            bytes = bytes.Length,
            contentType = response.Content.Headers.ContentType?.ToString()
        };
    }

    public static CliError BuildHttpError(HttpStatusCode statusCode, string? reasonPhrase, object? details)
    {
        var message = $"HTTP {(int)statusCode} {reasonPhrase}".Trim();
        return new CliError(CliErrorCode.Upstream, message, details, null);
    }

    public static object? ParsePayload(HttpResponseMessage response, byte[] payload)
    {
        if (payload.Length == 0)
        {
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.Clone();
            }
            catch
            {
                return Encoding.UTF8.GetString(payload);
            }
        }

        return Encoding.UTF8.GetString(payload);
    }

    public static string? TryGetRequestId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-arequestid", out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    public static bool TryExtractPage(JsonElement root, List<JsonElement> accumulated, out int nextStartAt, out bool isLast)
    {
        nextStartAt = 0;
        isLast = true;

        if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in values.EnumerateArray())
        {
            accumulated.Add(item.Clone());
        }

        var startAt = root.TryGetProperty("startAt", out var s) && s.TryGetInt32(out var sInt) ? sInt : 0;
        var maxResults = root.TryGetProperty("maxResults", out var m) && m.TryGetInt32(out var mInt) ? mInt : values.GetArrayLength();

        var total = 0;
        var totalKnown = root.TryGetProperty("total", out var t) && t.TryGetInt32(out total);
        var isLastFlag = root.TryGetProperty("isLast", out var il) && il.ValueKind == JsonValueKind.True;

        if (isLastFlag)
        {
            isLast = true;
            return true;
        }

        nextStartAt = startAt + maxResults;

        if (totalKnown)
        {
            isLast = nextStartAt >= total;
        }
        else
        {
            isLast = values.GetArrayLength() == 0;
        }

        return true;
    }

    public static string BuildCombinedOutput(JsonElement rootTemplate, List<JsonElement> items)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            if (rootTemplate.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in rootTemplate.EnumerateObject())
                {
                    if (property.NameEquals("values"))
                    {
                        continue;
                    }

                    property.WriteTo(writer);
                }
            }

            writer.WritePropertyName("values");
            writer.WriteStartArray();
            foreach (var item in items)
            {
                item.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
