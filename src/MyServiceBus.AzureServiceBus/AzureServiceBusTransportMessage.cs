using MyServiceBus.Transports;

namespace MyServiceBus.AzureServiceBus;

internal sealed class AzureServiceBusTransportMessage : ITransportMessage
{
    public AzureServiceBusTransportMessage(IDictionary<string, object> headers, byte[] payload)
    {
        Headers = headers;
        Payload = payload;
    }

    public IDictionary<string, object> Headers { get; }

    public bool IsDurable => true;

    public byte[] Payload { get; }
}
