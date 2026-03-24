using System.Threading.Tasks;

namespace MyServiceBus.Serialization;

public interface IMessageSerializer
{
    string ContentType { get; }

    MessageEnvelopeMode EnvelopeMode { get; }

    Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context)
        where T : class;
}
