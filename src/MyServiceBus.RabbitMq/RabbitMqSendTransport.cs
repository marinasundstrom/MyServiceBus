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
        var body = await context.Serialize(message);

        var props = _channel.CreateBasicProperties();
        props.Persistent = true;

        if (context.Headers != null)
        {
            try
            {
                if (context.Headers.TryGetValue("content_type", out var ct))
                    props.ContentType = ct.ToString();

                props.Headers = context.Headers.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value is string s ? (object)System.Text.Encoding.UTF8.GetBytes(s) : kv.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to map message headers: {ex}");
                props.Headers = new Dictionary<string, object?>();
            }
        }

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
