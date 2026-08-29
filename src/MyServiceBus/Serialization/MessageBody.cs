using System.IO;

namespace MyServiceBus.Serialization;

public interface MessageBody
{
    long? Length { get; }

    Stream GetStream();

    byte[] GetBytes();

    string GetString();
}
