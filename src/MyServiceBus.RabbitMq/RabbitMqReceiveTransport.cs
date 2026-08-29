using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly IInboundMessageResolver _inboundMessageResolver;
    private readonly IMessageHeaderConvention _headerConvention = MassTransitHeaderConvention.Instance;
    private readonly Uri? _errorAddress;
    private readonly Uri? _faultAddress;
    private readonly Func<string?, bool>? _isMessageTypeRegistered;
    private readonly ILogger<RabbitMqReceiveTransport>? _logger;
    private string _consumerTag;

    public RabbitMqReceiveTransport(IChannel channel, string queueName, Func<ReceiveContext, Task> handler, Uri? errorAddress, Uri? faultAddress, Func<string?, bool>? isMessageTypeRegistered, IInboundMessageResolver? inboundMessageResolver = null, ILogger<RabbitMqReceiveTransport>? logger = null)
    {
        _channel = channel;
        _queueName = queueName;
        _messageHandler = handler;
        _errorAddress = errorAddress;
        _faultAddress = faultAddress;
        _isMessageTypeRegistered = isMessageTypeRegistered;
        _inboundMessageResolver = inboundMessageResolver ?? new InboundMessageResolver();
        _logger = logger;
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
                    headers[_headerConvention.ContentTypeHeader] = props.ContentType!;
                else if (!headers.ContainsKey(_headerConvention.ContentTypeHeader))
                    headers[_headerConvention.ContentTypeHeader] = InboundMessageResolver.EnvelopeContentType;

                if (!string.IsNullOrWhiteSpace(props.MessageId))
                    headers["message_id"] = props.MessageId;
                if (!string.IsNullOrWhiteSpace(props.CorrelationId))
                    headers["correlation_id"] = props.CorrelationId;
                if (!string.IsNullOrWhiteSpace(props.ReplyTo))
                    headers["reply_to"] = props.ReplyTo;

                if (_faultAddress != null && !headers.ContainsKey(_headerConvention.FaultAddressHeader))
                    headers[_headerConvention.FaultAddressHeader] = _faultAddress.ToString();

                var transportMessage = new RabbitMqTransportMessage(headers, props.Persistent, payload);
                var messageContext = _inboundMessageResolver.Resolve(transportMessage);

                var context = new RabbitMqReceiveContext(messageContext, props, ea.DeliveryTag, ea.Exchange, ea.RoutingKey, _errorAddress);
                var messageType = context.MessageType.FirstOrDefault();
                if (_isMessageTypeRegistered != null && !_isMessageTypeRegistered(messageType))
                {
                    if (_errorAddress != null)
                    {
                        await _channel.BasicPublishAsync(
                            exchange: _queueName + "_skipped",
                            routingKey: string.Empty,
                            mandatory: true,
                            basicProperties: new BasicProperties(props),
                            body: payload);
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    return;
                }

                await _messageHandler.Invoke(context);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception exc)
            {
                _logger?.LogError(exc, "Message handling failed");
                try
                {
                    if (ErrorTransportSettlement.WasMoved(exc))
                    {
                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                    }
                }
                catch (Exception settlementException)
                {
                    _logger?.LogError(
                        settlementException,
                        "Failed to settle RabbitMQ delivery {DeliveryTag} from queue {QueueName}",
                        ea.DeliveryTag,
                        _queueName);
                }
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
