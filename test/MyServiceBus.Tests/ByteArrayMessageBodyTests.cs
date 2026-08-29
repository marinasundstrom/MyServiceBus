using System.Text;
using MyServiceBus.Serialization;

namespace MyServiceBus.Tests;

public class ByteArrayMessageBodyTests
{
    [Fact]
    public void Exposes_length_bytes_stream_and_text()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");
        var body = new ByteArrayMessageBody(bytes);

        Assert.Equal(bytes.Length, body.Length);
        Assert.Same(bytes, body.GetBytes());
        Assert.Equal("hello", body.GetString());
        using var stream = body.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal("hello", reader.ReadToEnd());
    }
}
