using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace MyServiceBus;

public sealed class RabbitMqQueueSendTransport : ISendTransport
{
    private readonly IChannel _channel;
    private readonly string _queue;

    public RabbitMqQueueSendTransport(IChannel channel, string queue)
    {
        _channel = channel;
        _queue = queue;
    }

    public async Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
        where T : class
    {
        var props = new BasicProperties
        {
            Persistent = true
        };

        var body = await context.Serialize(message);

        if (context.Headers != null)
        {
            try
            {
                props.Headers = context.Headers.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
                if (context.Headers.TryGetValue("content_type", out var ct))
                    props.ContentType = ct.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to map message headers: {ex}");
                props.Headers = new Dictionary<string, object?>();
            }
        }

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queue,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);
    }
}
