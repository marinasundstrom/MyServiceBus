using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Transports;

namespace MyServiceBus.RabbitMq;

internal class RabbitMqTransportMessage : ITransportMessage
{
    public RabbitMqTransportMessage(IDictionary<string, object?> headers, bool isDurable, byte[] payload)
    {
        Headers = headers?.ToDictionary(x => x.Key, x => x.Value ?? (object)string.Empty) ?? new Dictionary<string, object>();
        IsDurable = isDurable;
        Payload = payload;
    }

    public IDictionary<string, object> Headers { get; }

    public bool IsDurable { get; }

    public byte[] Payload { get; }
}
