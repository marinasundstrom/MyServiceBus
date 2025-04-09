using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MyServiceBus;

public sealed class RabbitMqReceiveTransport : IReceiveTransport
{
    private readonly IChannel _channel;
    private readonly string _queueName;
    private readonly IMessageHandler _messageHandler;
    private string _consumerTag;

    public RabbitMqReceiveTransport(IChannel channel, string queueName, IMessageHandler handler)
    {
        _channel = channel;
        _queueName = queueName;
        _messageHandler = handler;
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var props = ea.BasicProperties;

                var context = new ReceiveContextImpl(body, props.Headers?.ToDictionary(x => x.Key, x => (object)Encoding.UTF8.GetString((byte[])x.Value!)) ?? []);

                await _messageHandler.Handle(context);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception exc)
            {
                // Optionally nack
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);

                // Error handling & Retries
            }
        };

        _consumerTag = await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_consumerTag))
        {
            await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
        }
    }
}