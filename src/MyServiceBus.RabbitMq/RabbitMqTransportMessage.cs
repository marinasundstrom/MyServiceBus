using System;
using MyServiceBus.Transports;

namespace MyServiceBus.RabbitMq;

public class RabbitMqTransportMessage
{
    public RabbitMqTransportMessage(IDictionary<string, object?> headers, bool isDurable, byte[] payload)
    {
        Headers = headers;
        IsDurable = isDurable;
        Payload = payload;
    }

    public IDictionary<string, object> Headers { get; }

    public bool IsDurable { get; }

    public byte[] Payload { get; }
}
