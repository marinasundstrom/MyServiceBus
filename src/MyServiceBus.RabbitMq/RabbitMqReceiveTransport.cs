using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private readonly bool _hasErrorQueue;
    private string _consumerTag;

    public RabbitMqReceiveTransport(IChannel channel, string queueName, Func<ReceiveContext, Task> handler, bool hasErrorQueue)
    {
        _channel = channel;
        _queueName = queueName;
        _messageHandler = handler;
        _hasErrorQueue = hasErrorQueue;
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var payload = ea.Body.ToArray();
            var props = ea.BasicProperties;

            var headers = props.Headers?.ToDictionary(x => x.Key, x => (object)x.Value!) ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(props.ContentType))
                headers["content_type"] = props.ContentType!;
            else if (!headers.ContainsKey("content_type"))
                headers["content_type"] = "application/vnd.masstransit+json";

            var transportMessage = new RabbitMqTransportMessage(headers, props.Persistent, payload);
            var messageContext = _contextFactory.CreateMessageContext(transportMessage);

            var errorAddress = _hasErrorQueue
                ? new Uri($"rabbitmq://localhost/exchange/{_queueName}_error")
                : null;
            var context = new ReceiveContextImpl(messageContext, errorAddress);

            try
            {
                await _messageHandler.Invoke(context);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (UnknownMessageTypeException)
            {
                if (_hasErrorQueue)
                {
                    var propsOut = new BasicProperties
                    {
                        Persistent = true,
                        Headers = headers.ToDictionary(k => k.Key, v => v.Value is string s ? (object)Encoding.UTF8.GetBytes(s) : v.Value)
                    };
                    propsOut.Headers[MessageHeaders.Reason] = Encoding.UTF8.GetBytes("skip");
                    await _channel.BasicPublishAsync(
                        exchange: _queueName + "_skipped",
                        routingKey: string.Empty,
                        mandatory: false,
                        basicProperties: propsOut,
                        body: payload);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception exc)
            {
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
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
