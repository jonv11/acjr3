namespace Acjr3.OpenApi;

public sealed record OpenApiResult(bool Success, string Message, IReadOnlyList<string> Lines)
{
    public static OpenApiResult Ok(string message, IReadOnlyList<string>? lines = null) => new(true, message, lines ?? []);
    public static OpenApiResult Fail(string message) => new(false, message, []);
}
