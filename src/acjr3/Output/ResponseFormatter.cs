using System.Net;
using System.Text;
using System.Text.Json;

namespace Acjr3.Output;

public sealed class ResponseFormatter
{
    public string Format(HttpResponseMessage response, byte[] payload, bool raw, bool includeHeaders)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

        if (includeHeaders)
        {
            foreach (var header in response.Headers)
            {
                builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            foreach (var header in response.Content.Headers)
            {
                builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            builder.AppendLine();
        }

        if (payload.Length == 0 || response.StatusCode == HttpStatusCode.NoContent)
        {
            return builder.ToString().TrimEnd();
        }

        var rawText = Encoding.UTF8.GetString(payload);
        if (!raw && IsJson(response, rawText))
        {
            try
            {
                using var doc = JsonDocument.Parse(rawText);
                builder.AppendLine(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                builder.AppendLine(rawText);
            }
        }
        else
        {
            builder.AppendLine(rawText);
        }

        return builder.ToString().TrimEnd();
    }

    private static bool IsJson(HttpResponseMessage response, string text)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}


