using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class AmazonSqsReceiveTransport : IReceiveTransport
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;
    private readonly string? _skippedQueueUrl;
    private readonly string _queueName;
    private readonly bool _temporary;
    private readonly int _waitTimeSeconds;
    private readonly int _visibilityTimeoutSeconds;
    private readonly int _prefetchCount;
    private readonly int _concurrentMessageLimit;
    private readonly Func<ReceiveContext, Task> _handler;
    private readonly Func<string?, bool>? _isMessageTypeRegistered;
    private readonly Uri? _errorAddress;
    private readonly Uri? _faultAddress;
    private readonly IInboundMessageResolver _inboundMessageResolver;
    private readonly ILogger<AmazonSqsReceiveTransport>? _logger;
    private CancellationTokenSource? _stopping;
    private Task? _receiveLoop;

    internal AmazonSqsReceiveTransport(
        IAmazonSQS sqs,
        string queueUrl,
        string? skippedQueueUrl,
        string queueName,
        bool temporary,
        int waitTimeSeconds,
        int visibilityTimeoutSeconds,
        int prefetchCount,
        int concurrentMessageLimit,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered,
        Uri? errorAddress,
        Uri? faultAddress,
        IInboundMessageResolver inboundMessageResolver,
        ILogger<AmazonSqsReceiveTransport>? logger)
    {
        _sqs = sqs;
        _queueUrl = queueUrl;
        _skippedQueueUrl = skippedQueueUrl;
        _queueName = queueName;
        _temporary = temporary;
        _waitTimeSeconds = waitTimeSeconds;
        _visibilityTimeoutSeconds = visibilityTimeoutSeconds;
        _prefetchCount = prefetchCount;
        _concurrentMessageLimit = concurrentMessageLimit;
        _handler = handler;
        _isMessageTypeRegistered = isMessageTypeRegistered;
        _errorAddress = errorAddress;
        _faultAddress = faultAddress;
        _inboundMessageResolver = inboundMessageResolver;
        _logger = logger;
    }

    /// <exception cref="AmazonSqsTransportException">The receiver could not start.</exception>
    public Task Start(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_receiveLoop is not null)
            return Task.CompletedTask;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveLoop = ReceiveLoop(_stopping.Token);
        return Task.CompletedTask;
    }

    /// <exception cref="AmazonSqsTransportException">The receiver could not stop or clean up its temporary queue.</exception>
    public async Task Stop(CancellationToken cancellationToken = default)
    {
        var stopping = Interlocked.Exchange(ref _stopping, null);
        var loop = Interlocked.Exchange(ref _receiveLoop, null);
        if (stopping is null || loop is null)
            return;
        try
        {
            await stopping.CancelAsync().ConfigureAwait(false);
            await loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_temporary)
                await _sqs.DeleteQueueAsync(_queueUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not AmazonSqsTransportException)
        {
            throw new AmazonSqsTransportException("stop receive", _queueName, exception);
        }
        finally
        {
            stopping.Dispose();
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        using var availableDeliveries = new SemaphoreSlim(_prefetchCount);
        using var handlerConcurrency = new SemaphoreSlim(_concurrentMessageLimit);
        var active = new HashSet<Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await availableDeliveries.WaitAsync(cancellationToken).ConfigureAwait(false);
                var reserved = 1;
                while (reserved < Math.Min(10, _prefetchCount) && availableDeliveries.Wait(0))
                    reserved++;

                ReceiveMessageResponse response;
                try
                {
                    response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = reserved,
                        WaitTimeSeconds = _waitTimeSeconds,
                        VisibilityTimeout = _visibilityTimeoutSeconds,
                        MessageAttributeNames = ["All"],
                        MessageSystemAttributeNames = ["ApproximateReceiveCount"]
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    availableDeliveries.Release(reserved);
                    throw;
                }

                var unused = reserved - response.Messages.Count;
                if (unused > 0)
                    availableDeliveries.Release(unused);

                foreach (var message in response.Messages)
                {
                    var task = ProcessMessage(message, handlerConcurrency, cancellationToken).ContinueWith(
                        _ => availableDeliveries.Release(), CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    active.Add(task);
                }
                active.RemoveWhere(task => task.IsCompleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Amazon SQS receive loop failed for queue {QueueName}", _queueName);
            throw new AmazonSqsTransportException("receive", _queueName, exception);
        }
        finally
        {
            await Task.WhenAll(active).ConfigureAwait(false);
        }
    }

    private async Task ProcessMessage(
        Message message,
        SemaphoreSlim handlerConcurrency,
        CancellationToken cancellationToken)
    {
        using var renewalStopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewal = RenewVisibility(message.ReceiptHandle, renewalStopping.Token);
        try
        {
            var headers = AmazonSqsMessageMapper.CreateHeaders(message, _faultAddress);
            var transportMessage = new AmazonSqsTransportMessage(
                headers, System.Text.Encoding.UTF8.GetBytes(message.Body));
            var inboundMessage = _inboundMessageResolver.Resolve(transportMessage);
            var messageType = inboundMessage.MessageType.FirstOrDefault();

            if (_isMessageTypeRegistered is not null && !_isMessageTypeRegistered(messageType))
            {
                if (_skippedQueueUrl is not null)
                    await _sqs.SendMessageAsync(AmazonSqsMessageMapper.CreateSqsRequest(
                        _skippedQueueUrl, transportMessage.Payload,
                        headers[MassTransitHeaderConvention.Instance.ContentTypeHeader].ToString()!), cancellationToken)
                        .ConfigureAwait(false);
                await Delete(message, cancellationToken).ConfigureAwait(false);
                return;
            }

            await handlerConcurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _handler(new AmazonSqsReceiveContext(inboundMessage, message, _errorAddress, cancellationToken))
                    .ConfigureAwait(false);
            }
            finally
            {
                handlerConcurrency.Release();
            }
            await Delete(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ErrorTransportSettlement.WasMoved(exception))
        {
            await Delete(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Message handling failed on Amazon SQS queue {QueueName}", _queueName);
            try
            {
                await _sqs.ChangeMessageVisibilityAsync(_queueUrl, message.ReceiptHandle, 0, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception settlementException)
            {
                _logger?.LogError(settlementException,
                    "Failed to release message {MessageId} on Amazon SQS queue {QueueName}",
                    message.MessageId, _queueName);
            }
        }
        finally
        {
            await renewalStopping.CancelAsync().ConfigureAwait(false);
            try { await renewal.ConfigureAwait(false); }
            catch (OperationCanceledException) when (renewalStopping.IsCancellationRequested) { }
        }
    }

    private async Task RenewVisibility(string receiptHandle, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, _visibilityTimeoutSeconds / 2));
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            await _sqs.ChangeMessageVisibilityAsync(
                _queueUrl, receiptHandle, _visibilityTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task Delete(Message message, CancellationToken cancellationToken) =>
        _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, cancellationToken);
}
