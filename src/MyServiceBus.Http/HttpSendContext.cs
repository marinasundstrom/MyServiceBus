using System;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class HttpSendContext : SendContext
{
    public HttpSendContext(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        : base(messageTypes, serializer, cancellationToken)
    {
    }

    public string ContentType { get; set; } = "application/json";
}

