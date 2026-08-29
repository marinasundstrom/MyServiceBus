using System.IO;
using System.Text;

namespace MyServiceBus.Serialization;

public sealed class ByteArrayMessageBody : MessageBody
{
    private readonly byte[] _bytes;

    public ByteArrayMessageBody(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = bytes;
    }

    public long? Length => _bytes.LongLength;

    public Stream GetStream() => new MemoryStream(_bytes, writable: false);

    public byte[] GetBytes() => _bytes;

    public string GetString() => Encoding.UTF8.GetString(_bytes);
}
