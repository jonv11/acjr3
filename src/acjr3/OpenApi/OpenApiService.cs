using System.Text.Json;

namespace Acjr3.OpenApi;

public sealed class OpenApiService(IHttpClientFactory httpClientFactory)
{
    private static readonly string[] DefaultSpecUrls =
    [
        "https://dac-static.atlassian.com/cloud/jira/platform/swagger-v3.v3.json",
        "https://developer.atlassian.com/cloud/jira/platform/swagger-v3.v3.json"
    ];

    private static string ResolveCachePath()
    {
        var configured = Environment.GetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "acjr3");
        return Path.Combine(cacheDirectory, "openapi-v3.json");
    }

    public async Task<OpenApiResult> FetchAsync(string? outPath, string? specUrl, IAppLogger logger)
    {
        var urls = !string.IsNullOrWhiteSpace(specUrl) ? [specUrl] : DefaultSpecUrls;
        var client = httpClientFactory.CreateClient("acjr3");

        foreach (var url in urls)
        {
            try
            {
                logger.Verbose($"Fetching OpenAPI spec from {url}");
                using var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    logger.Verbose($"OpenAPI fetch failed from {url}: {(int)response.StatusCode}");
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (!doc.RootElement.TryGetProperty("paths", out _))
                {
                    logger.Verbose($"Downloaded document from {url} does not look like OpenAPI JSON.");
                    continue;
                }

                var target = string.IsNullOrWhiteSpace(outPath) ? ResolveCachePath() : outPath!;
                var directory = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(target, content);
                return OpenApiResult.Ok($"OpenAPI spec saved to {target}");
            }
            catch (Exception ex)
            {
                logger.Verbose($"OpenAPI fetch error from {url}: {ex.Message}");
            }
        }

        return OpenApiResult.Fail("Unable to fetch OpenAPI spec. Provide --spec-url or use local --spec-file for openapi paths/show.");
    }

    public OpenApiResult ListPaths(string? filter, string? specFile)
    {
        if (!TryLoadSpec(specFile, out var doc, out var error))
        {
            return OpenApiResult.Fail(error);
        }

        if (doc == null)
        {
            return OpenApiResult.Fail("OpenAPI document is null.");
        }
        using (doc)
        {
            var root = doc.RootElement;
            var lines = new List<string>();
            var lookup = filter?.Trim();

            foreach (var path in root.GetProperty("paths").EnumerateObject())
            {
                foreach (var method in path.Value.EnumerateObject())
                {
                    var opId = method.Value.TryGetProperty("operationId", out var operationId)
                        ? operationId.GetString() ?? string.Empty
                        : string.Empty;
                    var line = $"{method.Name.ToUpperInvariant(),-7} {path.Name}  ({opId})";

                    if (string.IsNullOrWhiteSpace(lookup)
                        || line.Contains(lookup, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add(line);
                    }
                }
            }

            if (lines.Count == 0)
            {
                return OpenApiResult.Ok("No matching operations found.", []);
            }

            lines.Sort(StringComparer.OrdinalIgnoreCase);
            return OpenApiResult.Ok($"Found {lines.Count} operations", lines);
        }
    }

    public OpenApiResult ShowOperation(string method, string path, string? specFile)
    {
        if (!TryLoadSpec(specFile, out var doc, out var error))
        {
            return OpenApiResult.Fail(error);
        }

        if (doc == null)
        {
            return OpenApiResult.Fail("OpenAPI document is null.");
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (!root.GetProperty("paths").TryGetProperty(path, out var pathNode))
            {
                return OpenApiResult.Fail($"Path not found: {path}");
            }

            if (!pathNode.TryGetProperty(method.ToLowerInvariant(), out var operation))
            {
                return OpenApiResult.Fail($"Method '{method.ToUpperInvariant()}' not found for path '{path}'.");
            }

            var operationIdText = operation.TryGetProperty("operationId", out var opId)
                ? opId.GetString()
                : "<none>";

            var lines = new List<string>
            {
                $"Method: {method.ToUpperInvariant()}",
                $"Path: {path}",
                $"operationId: {operationIdText}"
            };

            if (operation.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
            {
                var required = parameters
                    .EnumerateArray()
                    .Where(p => p.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True)
                    .Select(p =>
                    {
                        var paramIn = p.TryGetProperty("in", out var i) ? i.GetString() : "?";
                        var name = p.TryGetProperty("name", out var n) ? n.GetString() : "?";
                        return $"{paramIn}:{name}";
                    })
                    .ToArray();

                lines.Add(required.Length == 0
                    ? "Required params: <none>"
                    : $"Required params: {string.Join(", ", required)}");
            }
            else
            {
                lines.Add("Required params: <none>");
            }

            if (operation.TryGetProperty("requestBody", out var requestBody)
                && requestBody.TryGetProperty("content", out var requestContent)
                && requestContent.ValueKind == JsonValueKind.Object)
            {
                lines.Add("Request content-types: " + string.Join(", ", requestContent.EnumerateObject().Select(x => x.Name)));
            }
            else
            {
                lines.Add("Request content-types: <none>");
            }

            if (operation.TryGetProperty("responses", out var responses) && responses.ValueKind == JsonValueKind.Object)
            {
                var responseLines = new List<string>();
                foreach (var response in responses.EnumerateObject())
                {
                    var contentTypes = response.Value.TryGetProperty("content", out var content)
                        ? string.Join(", ", content.EnumerateObject().Select(x => x.Name))
                        : "<none>";
                    responseLines.Add($"{response.Name}: {contentTypes}");
                }

                lines.Add("Responses: " + string.Join(" | ", responseLines));
            }
            else
            {
                lines.Add("Responses: <none>");
            }

            return OpenApiResult.Ok("Operation details", lines);
        }
    }

    private bool TryLoadSpec(string? specFile, out JsonDocument? document, out string error)
    {
        document = null;
        error = string.Empty;

        var path = string.IsNullOrWhiteSpace(specFile) ? ResolveCachePath() : specFile;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = $"OpenAPI spec not found at '{path}'. Run 'acjr3 openapi fetch' or use --spec-file.";
            return false;
        }

        try
        {
            var content = File.ReadAllText(path);
            document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("paths", out _))
            {
                error = $"Spec file '{path}' does not contain a 'paths' object.";
                document.Dispose();
                document = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to load spec file '{path}': {ex.Message}";
            return false;
        }
    }
}


