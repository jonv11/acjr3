using System.Text;

namespace Acjr3.Tests.Common;

public sealed class TextFileInputTests
{
    [Fact]
    public void ReadAllTextNormalized_RemovesUtf8Bom()
    {
        var path = WriteTempFile([0xEF, 0xBB, 0xBF], "{\"k\":1}");
        try
        {
            var text = TextFileInput.ReadAllTextNormalized(path);
            Assert.Equal("{\"k\":1}", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadAllTextNormalized_ReadsUtf16LeWithBom()
    {
        var preamble = Encoding.Unicode.GetPreamble();
        var path = WriteTempFile(preamble, "hello");
        try
        {
            var text = TextFileInput.ReadAllTextNormalized(path);
            Assert.Equal("hello", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadAllTextNormalized_ReadsUtf16BeWithBom()
    {
        var preamble = Encoding.BigEndianUnicode.GetPreamble();
        var path = WriteTempFile(preamble, "hello");
        try
        {
            var text = TextFileInput.ReadAllTextNormalized(path);
            Assert.Equal("hello", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadAllTextNormalized_LeavesNonBomTextUnchanged()
    {
        var path = WriteTempFile([], "plain-text");
        try
        {
            var text = TextFileInput.ReadAllTextNormalized(path);
            Assert.Equal("plain-text", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempFile(byte[] preamble, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"acjr3-text-input-{Guid.NewGuid():N}.txt");
        byte[] body;
        if (preamble.SequenceEqual(Encoding.Unicode.GetPreamble()))
        {
            body = Encoding.Unicode.GetBytes(content);
        }
        else if (preamble.SequenceEqual(Encoding.BigEndianUnicode.GetPreamble()))
        {
            body = Encoding.BigEndianUnicode.GetBytes(content);
        }
        else
        {
            body = Encoding.UTF8.GetBytes(content);
        }

        File.WriteAllBytes(path, [.. preamble, .. body]);
        return path;
    }
}
