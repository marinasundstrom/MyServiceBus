using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.RabbitMq;
using MyServiceBus.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MyServiceBus;

public sealed class RabbitMqReceiveTransport : IReceiveTransport
{
    private readonly IChannel _channel;
    private readonly string _queueName;
    private readonly Func<ReceiveContext, Task> _messageHandler;
    private readonly MessageContextFactory _contextFactory = new();
    private string _consumerTag;

    public RabbitMqReceiveTransport(IChannel channel, string queueName, Func<ReceiveContext, Task> handler)
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
                var payload = ea.Body.ToArray();
                var props = ea.BasicProperties;

                var headers = props.Headers?.ToDictionary(x => x.Key, x => (object)x.Value!) ?? new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(props.ContentType))
                {
                    headers["content_type"] = props.ContentType!;
                }
                else if (!headers.ContainsKey("content_type"))
                {
                    headers["content_type"] = "application/vnd.mybus.envelope+json";
                }

                var transportMessage = new RabbitMqTransportMessage(headers, props.Persistent, payload);
                var messageContext = _contextFactory.CreateMessageContext(transportMessage);

                var context = new ReceiveContextImpl(messageContext);

                await _messageHandler.Invoke(context);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception exc)
            {
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                Console.WriteLine($"Message handling failed: {exc}");
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
