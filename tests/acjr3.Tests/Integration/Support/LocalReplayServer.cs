using System.Net.Sockets;
using System.Text;

namespace Acjr3.Tests.Integration;

internal sealed record ReplayResponse(int StatusCode, string ReasonPhrase, IReadOnlyDictionary<string, string> Headers, string Body);
internal sealed record ReplayNameValue(string Key, string Value);
internal sealed record RecordedRequest(string Method, string Path, List<ReplayNameValue> Query, List<ReplayNameValue> Headers, string Accept, string? ContentType, string? Body);
internal sealed record ReplayRequest(string Method, string Path, string Body, IReadOnlyDictionary<string, string> Headers);

internal sealed class LocalReplayServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
    private readonly Queue<ReplayResponse> responses = new();
    private readonly List<ReplayRequest> requests = [];
    private readonly CancellationTokenSource cts = new();
    private readonly object sync = new();
    private Task? loopTask;

    public string BaseUrl { get; private set; } = string.Empty;
    public ReplayRequest? LastRequest
    {
        get
        {
            lock (sync)
            {
                return requests.Count == 0 ? null : requests[^1];
            }
        }
    }

    public void EnqueueResponse(ReplayResponse response) => responses.Enqueue(response);

    public Task StartAsync()
    {
        listener.Start();
        var endpoint = (System.Net.IPEndPoint)listener.LocalEndpoint;
        BaseUrl = $"http://127.0.0.1:{endpoint.Port}";
        loopTask = Task.Run(() => AcceptLoopAsync(cts.Token));
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientHandle = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        var requestParts = (requestLine ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var method = requestParts.Length > 0 ? requestParts[0] : string.Empty;
        var path = requestParts.Length > 1 ? requestParts[1] : string.Empty;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null || line.Length == 0)
            {
                break;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            headers[key] = value;
        }

        var body = string.Empty;
        if (headers.TryGetValue("Content-Length", out var contentLengthRaw)
            && int.TryParse(contentLengthRaw, out var contentLength)
            && contentLength > 0)
        {
            var bodyBuffer = new char[contentLength];
            var read = 0;
            while (read < contentLength)
            {
                var count = await reader.ReadBlockAsync(bodyBuffer.AsMemory(read, contentLength - read), cancellationToken);
                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            body = new string(bodyBuffer, 0, read);
        }

        lock (sync)
        {
            requests.Add(new ReplayRequest(method, path, body, headers));
        }

        if (responses.Count == 0)
        {
            await WriteResponseAsync(writer, new ReplayResponse(500, "No Response Scripted", new Dictionary<string, string>(), "{\"error\":\"no-response\"}"));
            return;
        }

        await WriteResponseAsync(writer, responses.Dequeue());
    }

    private static async Task WriteResponseAsync(StreamWriter writer, ReplayResponse response)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);
        await writer.WriteLineAsync($"HTTP/1.1 {response.StatusCode} {response.ReasonPhrase}");
        foreach (var header in response.Headers)
        {
            await writer.WriteLineAsync($"{header.Key}: {header.Value}");
        }

        if (!response.Headers.ContainsKey("Content-Length"))
        {
            await writer.WriteLineAsync($"Content-Length: {bodyBytes.Length}");
        }

        await writer.WriteLineAsync("Connection: close");
        await writer.WriteLineAsync();
        await writer.BaseStream.WriteAsync(bodyBytes);
        await writer.BaseStream.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        listener.Stop();
        if (loopTask != null)
        {
            try
            {
                await loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
    }
}
