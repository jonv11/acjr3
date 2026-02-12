using System.Text;

namespace Acjr3.Common;

public static class TextFileInput
{
    public static async Task<string> ReadAllTextNormalizedAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return DecodeAndNormalize(bytes);
    }

    public static string ReadAllTextNormalized(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return DecodeAndNormalize(bytes);
    }

    private static string DecodeAndNormalize(byte[] bytes)
    {
        string text;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else
        {
            text = Encoding.UTF8.GetString(bytes);
        }

        return text.Length > 0 && text[0] == '\uFEFF'
            ? text[1..]
            : text;
    }
}
