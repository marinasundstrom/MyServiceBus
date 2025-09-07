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

    [Throws(typeof(IOException))]
    public async Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
        where T : class
    {
        var body = await context.Serialize(message);

        var rabbitContext = context as RabbitMqSendContext;
        var props = rabbitContext?.Properties ?? new BasicProperties { Persistent = true };

        if (context.Headers != null)
        {
            try
            {
                Dictionary<string, object?>? headers = null;

                foreach (var kv in context.Headers)
                {
                    if (kv.Key.StartsWith("_"))
                    {
                        var key = kv.Key[1..];
                        var value = kv.Value?.ToString();

                        switch (key)
                        {
                            case "content_type":
                                props.ContentType = value;
                                break;
                            case "correlation_id":
                                props.CorrelationId = value;
                                break;
                            case "message_id":
                                props.MessageId = value;
                                break;
                            case "reply_to":
                                props.ReplyTo = value;
                                break;
                            case "type":
                                props.Type = value;
                                break;
                            case "user_id":
                                props.UserId = value;
                                break;
                            case "app_id":
                                props.AppId = value;
                                break;
                            case "expiration":
                                props.Expiration = value;
                                break;
                            default:
                                headers ??= new Dictionary<string, object?>();
                                headers[key] = kv.Value is string s ? System.Text.Encoding.UTF8.GetBytes(s) : kv.Value;
                                break;
                        }
                    }
                    else
                    {
                        headers ??= new Dictionary<string, object?>();
                        headers[kv.Key] = kv.Value is string s ? System.Text.Encoding.UTF8.GetBytes(s) : kv.Value;
                    }
                }

                if (headers != null && headers.Count > 0)
                    props.Headers = headers;
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
