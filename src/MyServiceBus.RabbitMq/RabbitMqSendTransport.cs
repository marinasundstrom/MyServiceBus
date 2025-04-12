using RabbitMQ.Client;

namespace MyServiceBus;

public sealed class RabbitMqSendTransport : ISendTransport
{
    private readonly IChannel _channel;
    private readonly string _exchange;

    public RabbitMqSendTransport(IChannel channel, string exchange)
    {
        _channel = channel;
        _exchange = exchange;
    }

    public async Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
        where T : class
    {
        var props = new BasicProperties();

        props.Persistent = true;

        // Headers
        if (context.Headers != null)
        {
            props.Headers = context.Headers.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        }

        var body = await context.Serialize(message); // assume JSON or similar

        await _channel.BasicPublishAsync(
            exchange: _exchange,
            routingKey: context.RoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken
        );
    }
}
