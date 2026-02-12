using System.Text;

namespace Acjr3.Common;

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
