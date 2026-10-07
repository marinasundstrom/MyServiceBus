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
    private readonly IBusHookDispatcher? _hooks;
    private readonly SemaphoreSlim _concurrency;
    private readonly object _lifecycleSync = new();
    private TaskCompletionSource<bool> _drained = CompletedDrain();
    private int _activeDeliveries;
    private bool _stopping;
    private string _consumerTag;

    public RabbitMqReceiveTransport(IChannel channel, string queueName, Func<ReceiveContext, Task> handler, Uri? errorAddress, Uri? faultAddress, Func<string?, bool>? isMessageTypeRegistered, IInboundMessageResolver? inboundMessageResolver = null, ILogger<RabbitMqReceiveTransport>? logger = null, int concurrentMessageLimit = 1, IBusHookDispatcher? hooks = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrentMessageLimit, 1);
        _channel = channel;
        _queueName = queueName;
        _messageHandler = handler;
        _errorAddress = errorAddress;
        _faultAddress = faultAddress;
        _isMessageTypeRegistered = isMessageTypeRegistered;
        _inboundMessageResolver = inboundMessageResolver ?? new InboundMessageResolver();
        _logger = logger;
        _hooks = hooks;
        _concurrency = new SemaphoreSlim(concurrentMessageLimit, concurrentMessageLimit);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleSync)
        {
            _stopping = false;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            if (!TryBeginDelivery())
            {
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                return;
            }

            var handlerOwnsCompletion = false;
            try
            {
                await _concurrency.WaitAsync().ConfigureAwait(false);
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
                if (_isMessageTypeRegistered != null && !(context.MessageType.Count == 0
                        ? _isMessageTypeRegistered(null)
                        : context.MessageType.Any(type => _isMessageTypeRegistered(type))))
                {
                    _logger?.LogWarning("Skipping message {MessageId} on {Endpoint}: no matching contract among {MessageTypes}",
                        context.MessageId, _queueName, string.Join(", ", context.MessageType));
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
                    _hooks?.Dispatch(new MessageSkippedHookEvent(DateTimeOffset.UtcNow, _queueName,
                        context.MessageId.ToString(), context.MessageType.ToArray()));
                    return;
                }

                var handling = _messageHandler.Invoke(context);
                handlerOwnsCompletion = true;
                _ = CompleteDeliveryAsync(ea.DeliveryTag, handling, payload, props);
                return;
            }
            catch (Exception exc)
            {
                await SettleFailureAsync(ea.DeliveryTag, exc, ea.Body.ToArray(), ea.BasicProperties).ConfigureAwait(false);
            }
            finally
            {
                if (!handlerOwnsCompletion)
                    EndDelivery();
            }
        };

        _consumerTag = await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }

    private async Task CompleteDeliveryAsync(ulong deliveryTag, Task handling, byte[] payload, IReadOnlyBasicProperties properties)
    {
        try
        {
            await handling.ConfigureAwait(false);
            await _channel.BasicAckAsync(deliveryTag, multiple: false).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await SettleFailureAsync(deliveryTag, exception, payload, properties).ConfigureAwait(false);
        }
        finally
        {
            EndDelivery();
        }
    }

    private async Task SettleFailureAsync(ulong deliveryTag, Exception exception, byte[] payload, IReadOnlyBasicProperties properties)
    {
        _logger?.LogError(exception, "Message handling failed");
        try
        {
            if (_errorAddress is not null && exception is MessageDeserializationException or JsonException)
            {
                var errorProperties = new BasicProperties(properties);
                errorProperties.Headers = properties.Headers is null
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?>(properties.Headers);
                errorProperties.Headers[MessageHeaders.ExceptionType] = exception.GetType().FullName;
                errorProperties.Headers[MessageHeaders.ExceptionMessage] = exception.ToString();
                errorProperties.Headers[MessageHeaders.Reason] = "fault";
                await _channel.BasicPublishAsync(_queueName + "_error", "", true, errorProperties, payload);
                ErrorTransportSettlement.MarkMoved(exception, _errorAddress);
            }
            if (ErrorTransportSettlement.WasMoved(exception))
            {
                await _channel.BasicAckAsync(deliveryTag, multiple: false).ConfigureAwait(false);
            }
            else
            {
                await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true).ConfigureAwait(false);
            }
        }
        catch (Exception settlementException)
        {
            _logger?.LogError(
                settlementException,
                "Failed to settle RabbitMQ delivery {DeliveryTag} from queue {QueueName}",
                deliveryTag,
                _queueName);
            await AbortChannelAsync().ConfigureAwait(false); // Unsettled deliveries are requeued by the broker.
        }
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleSync)
        {
            _stopping = true;
        }

        try
        {
            if (!string.IsNullOrEmpty(_consumerTag))
            {
                await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            Task drainTask;
            lock (_lifecycleSync)
            {
                drainTask = _drained.Task;
            }

            await drainTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = AbortChannelAsync();
            throw;
        }
    }

    private async Task AbortChannelAsync()
    {
        try
        {
            await _channel.AbortAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Failed to abort RabbitMQ channel for queue {QueueName}", _queueName);
        }
    }

    private bool TryBeginDelivery()
    {
        lock (_lifecycleSync)
        {
            if (_stopping)
                return false;

            if (_activeDeliveries++ == 0)
            {
                _drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return true;
        }
    }

    private void EndDelivery()
    {
        TaskCompletionSource<bool>? drained = null;
        lock (_lifecycleSync)
        {
            if (--_activeDeliveries == 0)
                drained = _drained;
        }

        drained?.TrySetResult(true);
        _concurrency.Release();
    }

    private static TaskCompletionSource<bool> CompletedDrain()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        completion.SetResult(true);
        return completion;
    }
}
