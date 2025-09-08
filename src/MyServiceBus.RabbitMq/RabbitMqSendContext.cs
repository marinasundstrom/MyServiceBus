using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using MyServiceBus.Serialization;
using RabbitMQ.Client;

namespace MyServiceBus;

public class RabbitMqSendContext : SendContext, IQueueSendContext
{
    public RabbitMqSendContext(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        : base(messageTypes, serializer, cancellationToken)
    {
        Properties = new BasicProperties
        {
            Persistent = true
        };
    }

    public BasicProperties Properties { get; }

    public TimeSpan? TimeToLive
    {
        get => long.TryParse(Properties.Expiration, out var ms) ? TimeSpan.FromMilliseconds(ms) : null;
        set => Properties.Expiration = value.HasValue
            ? ((long)value.Value.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)
            : null;
    }

    public bool Persistent
    {
        get => Properties.Persistent;
        set => Properties.Persistent = value;
    }

    public IDictionary<string, object> BrokerProperties =>
        Properties.Headers ??= new Dictionary<string, object>();
}

public class RabbitMqSendContextFactory : ISendContextFactory
{
    public SendContext Create(Type[] messageTypes, IMessageSerializer serializer, CancellationToken cancellationToken = default)
        => new RabbitMqSendContext(messageTypes, serializer, cancellationToken);
}

