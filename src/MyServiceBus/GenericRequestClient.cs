using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using System.Reflection;

namespace MyServiceBus;

public sealed class GenericRequestClient<TRequest> : IRequestClient<TRequest>, IDisposable
    where TRequest : class
{
    private readonly ITransportFactory _transportFactory;
    private readonly IMessageSerializer _serializer;
    private readonly Uri? _destinationAddress;
    private readonly RequestTimeout _timeout;
    private readonly ISendContextFactory _sendContextFactory;

    public GenericRequestClient(
        ITransportFactory transportFactory,
        IMessageSerializer serializer,
        ISendContextFactory sendContextFactory,
        Uri? destinationAddress = null,
        RequestTimeout timeout = default)
    {
        _transportFactory = transportFactory;
        _serializer = serializer;
        _sendContextFactory = sendContextFactory;
        _destinationAddress = destinationAddress;
        _timeout = timeout.TimeSpan == default ? RequestTimeout.Default : timeout;
    }

    public void Dispose()
    {

    }

    public async Task<Response<T>> GetResponseAsync<T>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default) where T : class
    {
        var taskCompletionSource = new TaskCompletionSource<Response<T>>();

        var responseExchange = $"resp-{Guid.NewGuid():N}";
        var responseReceiveTopology = new ReceiveEndpointTransportTopology(
            responseExchange,
            durable: false,
            temporary: true,
            prefetchCount: 0,
            [new MessageBinding { MessageType = typeof(T), EntityName = responseExchange }]);

        IReceiveTransport? responseReceiveTransport = null;

        var responseHandler = async (ReceiveContext context) =>
        {
            try
            {
                if (context.MessageType.Contains(MessageUrn.For(typeof(T))) &&
                    context.TryGetMessage<T>(out var responeMessage))
                {
                    var response = new Response<T>(responeMessage);
                    taskCompletionSource.TrySetResult(response);
                    return;
                }

                if (context.MessageType.Contains(MessageUrn.For(typeof(Fault<TRequest>))) &&
                    context.TryGetMessage<Fault<TRequest>>(out var fault))
                {
                    taskCompletionSource.TrySetException(new RequestFaultException(typeof(TRequest).Name, fault!));
                }
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }
        };

        responseReceiveTransport = await _transportFactory.CreateReceiveTransport(responseReceiveTopology, responseHandler, null, cancellationToken);

        await responseReceiveTransport.Start(cancellationToken);

        var requestAddress = _destinationAddress ?? _transportFactory.GetPublishAddress(EntityNameFormatter.Format(request.GetType()));
        var requestSendTransport = await _transportFactory.GetSendTransport(requestAddress, cancellationToken);

        var responseAddress = _transportFactory.GetTemporaryEndpointAddress(responseExchange);
        var sendContext = _sendContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(TRequest)), _serializer, cancellationToken);
        sendContext.ResponseAddress = responseAddress;
        sendContext.FaultAddress = responseAddress;
        sendContext.MessageId = Guid.NewGuid().ToString();
        sendContext.RequestId = Guid.NewGuid();

        contextCallback?.Invoke(sendContext);

        await requestSendTransport.Send(request, sendContext, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var actualTimeout = timeout.TimeSpan == default ? _timeout.TimeSpan : timeout.TimeSpan;
        timeoutCts.CancelAfter(actualTimeout);

        await using var registration = timeoutCts.Token.Register(() =>
        {
            taskCompletionSource.TrySetException(new TimeoutException("Request timed out."));
        });

        try
        {
            return await taskCompletionSource.Task;
        }
        finally
        {
            await responseReceiveTransport.Stop(cancellationToken);
        }
    }

    public async Task<Response<T1, T2>> GetResponseAsync<T1, T2>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T1 : class
        where T2 : class
    {
        var taskCompletionSource = new TaskCompletionSource<Response<T1, T2>>();

        var responseExchange = $"resp-{Guid.NewGuid():N}";
        var responseReceiveTopology = new ReceiveEndpointTransportTopology(
            responseExchange,
            durable: false,
            temporary: true,
            prefetchCount: 0,
            [new MessageBinding { MessageType = typeof(T1), EntityName = responseExchange }]);

        IReceiveTransport? responseReceiveTransport = null;

        var responseHandler = async (ReceiveContext context) =>
        {
            try
            {
                if (context.MessageType.Contains(MessageUrn.For(typeof(T1))) &&
                    context.TryGetMessage<T1>(out var message1))
                {
                    taskCompletionSource.TrySetResult(Response<T1, T2>.FromT1(message1));
                    return;
                }

                if (context.MessageType.Contains(MessageUrn.For(typeof(T2))) &&
                    context.TryGetMessage<T2>(out var message2))
                {
                    taskCompletionSource.TrySetResult(Response<T1, T2>.FromT2(message2));
                    return;
                }

                if (!typeof(T1).IsAssignableFrom(typeof(Fault<TRequest>)) &&
                    !typeof(T2).IsAssignableFrom(typeof(Fault<TRequest>)) &&
                    context.MessageType.Contains(MessageUrn.For(typeof(Fault<TRequest>))) &&
                    context.TryGetMessage<Fault<TRequest>>(out var fault))
                {
                    taskCompletionSource.TrySetException(new RequestFaultException(typeof(TRequest).Name, fault));
                }
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }
        };

        responseReceiveTransport = await _transportFactory.CreateReceiveTransport(responseReceiveTopology, responseHandler, null, cancellationToken);

        await responseReceiveTransport.Start(cancellationToken);

        var requestAddress = _destinationAddress ?? _transportFactory.GetPublishAddress(EntityNameFormatter.Format(request.GetType()));
        var requestSendTransport = await _transportFactory.GetSendTransport(requestAddress, cancellationToken);

        var responseAddress = _transportFactory.GetTemporaryEndpointAddress(responseExchange);
        var sendContext = _sendContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(TRequest)), _serializer, cancellationToken);
        sendContext.ResponseAddress = responseAddress;
        sendContext.FaultAddress = responseAddress;
        sendContext.MessageId = Guid.NewGuid().ToString();
        sendContext.RequestId = Guid.NewGuid();

        contextCallback?.Invoke(sendContext);

        await requestSendTransport.Send(request, sendContext, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var actualTimeout = timeout.TimeSpan == default ? _timeout.TimeSpan : timeout.TimeSpan;
        timeoutCts.CancelAfter(actualTimeout);

        await using var registration = timeoutCts.Token.Register(() =>
        {
            taskCompletionSource.TrySetException(new TimeoutException("Request timed out."));
        });

        try
        {
            return await taskCompletionSource.Task;
        }
        finally
        {
            await responseReceiveTransport.Stop(cancellationToken);
        }
    }
}
