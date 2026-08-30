using MyServiceBus.Transports;

namespace MyServiceBus;

internal sealed class AmazonSqsTransportMessage : ITransportMessage
{
    public AmazonSqsTransportMessage(IDictionary<string, object> headers, byte[] payload)
    {
        Headers = headers;
        Payload = payload;
    }

    public IDictionary<string, object> Headers { get; }
    public bool IsDurable => true;
    public byte[] Payload { get; }
}
