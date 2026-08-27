using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using MyServiceBus.AzureServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class AzureServiceBusReceiveTransport : IReceiveTransport
{
    private readonly ServiceBusProcessor _processor;
    private readonly ServiceBusSender _skippedSender;
    private readonly string _queueName;
    private readonly Func<ReceiveContext, Task> _handler;
    private readonly Func<string?, bool>? _isMessageTypeRegistered;
    private readonly Uri? _errorAddress;
    private readonly Uri? _faultAddress;
    private readonly IInboundMessageResolver _inboundMessageResolver = new InboundMessageResolver();
    private readonly ILogger<AzureServiceBusReceiveTransport>? _logger;

    internal AzureServiceBusReceiveTransport(
        ServiceBusProcessor processor,
        ServiceBusSender skippedSender,
        string queueName,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered,
        Uri? errorAddress,
        Uri? faultAddress,
        ILogger<AzureServiceBusReceiveTransport>? logger = null)
    {
        _processor = processor;
        _skippedSender = skippedSender;
        _queueName = queueName;
        _handler = handler;
        _isMessageTypeRegistered = isMessageTypeRegistered;
        _errorAddress = errorAddress;
        _faultAddress = faultAddress;
        _logger = logger;
        _processor.ProcessMessageAsync += ProcessMessage;
        _processor.ProcessErrorAsync += ProcessError;
    }

    /// <exception cref="AzureServiceBusTransportException">
    /// The processor could not start receiving from the configured queue.
    /// </exception>
    public async Task Start(CancellationToken cancellationToken = default)
    {
        try
        {
            await _processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AzureServiceBusTransportException("start receive", _queueName, exception);
        }
    }

    /// <exception cref="AzureServiceBusTransportException">
    /// The processor could not stop receiving from the configured queue.
    /// </exception>
    public async Task Stop(CancellationToken cancellationToken = default)
    {
        try
        {
            await _processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AzureServiceBusTransportException("stop receive", _queueName, exception);
        }
    }

    private async Task ProcessMessage(ProcessMessageEventArgs args)
    {
        try
        {
            var headers = AzureServiceBusMessageMapper.CreateHeaders(args.Message, _faultAddress);
            var transportMessage = new AzureServiceBusTransportMessage(headers, args.Message.Body.ToArray());
            var inboundMessage = _inboundMessageResolver.Resolve(transportMessage);
            var messageType = inboundMessage.MessageType.FirstOrDefault();

            if (_isMessageTypeRegistered is not null && !_isMessageTypeRegistered(messageType))
            {
                await _skippedSender.SendMessageAsync(
                    AzureServiceBusMessageMapper.Copy(args.Message),
                    args.CancellationToken).ConfigureAwait(false);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
                return;
            }

            var context = new AzureServiceBusReceiveContext(
                inboundMessage,
                args.Message,
                _errorAddress,
                args.CancellationToken);
            await _handler(context).ConfigureAwait(false);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            // Shutdown releases the lock for another receiver.
        }
        catch (Exception exception) when (ErrorTransportSettlement.WasMoved(exception))
        {
            await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Message handling failed on Azure Service Bus queue {QueueName}", _queueName);
            try
            {
                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception settlementException)
            {
                _logger?.LogError(
                    settlementException,
                    "Failed to abandon message {MessageId} on Azure Service Bus queue {QueueName}",
                    args.Message.MessageId,
                    _queueName);
            }
        }
    }

    private Task ProcessError(ProcessErrorEventArgs args)
    {
        _logger?.LogError(
            args.Exception,
            "Azure Service Bus processor error for {EntityPath} during {ErrorSource}",
            args.EntityPath,
            args.ErrorSource);
        return Task.CompletedTask;
    }
}
