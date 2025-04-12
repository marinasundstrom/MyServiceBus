using System;

namespace MyServiceBus.Transports;

public interface ITransportMessage
{
    public IDictionary<string, object> Headers { get; }

    public bool IsDurable { get; }

    public byte[] Payload { get; }
}
