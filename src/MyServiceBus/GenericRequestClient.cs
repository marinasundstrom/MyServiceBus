using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using System.Diagnostics;
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
    private readonly IBusHookDispatcher? _hooks;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public GenericRequestClient(
        ITransportFactory transportFactory,
        IMessageSerializer serializer,
        ISendContextFactory sendContextFactory,
        IBusHookDispatcher hooks)
        : this(transportFactory, serializer, sendContextFactory, destinationAddress: null, timeout: default, hooks: hooks)
    {
    }

    public GenericRequestClient(
        ITransportFactory transportFactory,
        IMessageSerializer serializer,
        ISendContextFactory sendContextFactory,
        Uri? destinationAddress = null,
        RequestTimeout timeout = default,
        IBusHookDispatcher? hooks = null)
    {
        _transportFactory = transportFactory;
        _serializer = serializer;
        _sendContextFactory = sendContextFactory;
        _hooks = hooks;
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
        var requestId = Guid.NewGuid();

        var responseHandler = async (ReceiveContext context) =>
        {
            try
            {
                if (context.RequestId != requestId)
                {
                    return;
                }

                if (context.MessageType.Contains(MessageUrn.For(typeof(T))) &&
                    context.TryGetMessage<T>(out var responeMessage))
                {
                    DispatchResponseObservation(context, responseExchange, responeMessage!);
                    var response = new Response<T>(responeMessage);
                    taskCompletionSource.TrySetResult(response);
                    return;
                }

                if (context.MessageType.Contains(MessageUrn.For(typeof(Fault<TRequest>))) &&
                    context.TryGetMessage<Fault<TRequest>>(out var fault))
                {
                    DispatchResponseObservation(context, responseExchange, fault!);
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

        var requestAddress = _destinationAddress ?? _transportFactory.GetPublishAddress(request.GetType());
        var requestSendTransport = await _transportFactory.GetSendTransport(requestAddress, cancellationToken);

        var responseAddress = _transportFactory.GetTemporaryEndpointAddress(responseExchange);
        var sendContext = _sendContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(TRequest)), _serializer, cancellationToken);
        sendContext.ResponseAddress = responseAddress;
        sendContext.FaultAddress = responseAddress;
        sendContext.MessageId = Guid.NewGuid().ToString();
        sendContext.RequestId = requestId;

        contextCallback?.Invoke(sendContext);
        requestId = sendContext.RequestId ?? requestId;
        sendContext.RequestId = requestId;

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await requestSendTransport.Send(request, sendContext, cancellationToken);
            DispatchRequestObservation("sent", true, request, requestAddress, sendContext, Stopwatch.GetElapsedTime(startedAt));
        }
        catch (Exception exception)
        {
            DispatchRequestObservation("send_faulted", false, request, requestAddress, sendContext, Stopwatch.GetElapsedTime(startedAt), exception);
            throw;
        }

        var actualTimeout = timeout.TimeSpan == default ? _timeout.TimeSpan : timeout.TimeSpan;

        try
        {
            return actualTimeout == RequestTimeout.None.TimeSpan
                ? await taskCompletionSource.Task.WaitAsync(cancellationToken)
                : await taskCompletionSource.Task.WaitAsync(actualTimeout, cancellationToken);
        }
        finally
        {
            await responseReceiveTransport.Stop(CancellationToken.None);
        }
    }

    private void DispatchRequestObservation(
        string kind,
        bool succeeded,
        TRequest message,
        Uri destinationAddress,
        SendContext context,
        TimeSpan duration,
        Exception? exception = null)
    {
        if (_hooks?.IsEnabled != true)
            return;

        _hooks.Dispatch(MessageOperationHookEvent.Create(
            kind,
            succeeded,
            typeof(TRequest).FullName ?? typeof(TRequest).Name,
            MessageUrn.For(typeof(TRequest)),
            null,
            destinationAddress.ToString(),
            duration,
            exception,
            context.CorrelationId,
            context.ConversationId?.ToString(),
            messageId: context.MessageId,
            requestId: context.RequestId?.ToString(),
            responseAddress: context.ResponseAddress?.ToString(),
            messageIntent: context.Intent.ToString(),
            message: message));
    }

    private void DispatchResponseObservation<TResponse>(ReceiveContext context, string endpointName, TResponse message)
        where TResponse : class
    {
        if (_hooks?.IsEnabled != true)
            return;

        _hooks.Dispatch(MessageOperationHookEvent.Create(
            "consumed",
            true,
            typeof(TResponse).FullName ?? typeof(TResponse).Name,
            MessageUrn.For(typeof(TResponse)),
            endpointName,
            null,
            TimeSpan.Zero,
            correlationId: context.CorrelationId?.ToString(),
            conversationId: context.ConversationId?.ToString(),
            messageId: context.MessageId.ToString(),
            requestId: context.RequestId?.ToString(),
            message: message));
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
        var requestId = Guid.NewGuid();

        var responseHandler = async (ReceiveContext context) =>
        {
            try
            {
                if (context.RequestId != requestId)
                {
                    return;
                }

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

        var requestAddress = _destinationAddress ?? _transportFactory.GetPublishAddress(request.GetType());
        var requestSendTransport = await _transportFactory.GetSendTransport(requestAddress, cancellationToken);

        var responseAddress = _transportFactory.GetTemporaryEndpointAddress(responseExchange);
        var sendContext = _sendContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(TRequest)), _serializer, cancellationToken);
        sendContext.ResponseAddress = responseAddress;
        sendContext.FaultAddress = responseAddress;
        sendContext.MessageId = Guid.NewGuid().ToString();
        sendContext.RequestId = requestId;

        contextCallback?.Invoke(sendContext);
        requestId = sendContext.RequestId ?? requestId;
        sendContext.RequestId = requestId;

        await requestSendTransport.Send(request, sendContext, cancellationToken);

        var actualTimeout = timeout.TimeSpan == default ? _timeout.TimeSpan : timeout.TimeSpan;

        try
        {
            return actualTimeout == RequestTimeout.None.TimeSpan
                ? await taskCompletionSource.Task.WaitAsync(cancellationToken)
                : await taskCompletionSource.Task.WaitAsync(actualTimeout, cancellationToken);
        }
        finally
        {
            await responseReceiveTransport.Stop(CancellationToken.None);
        }
    }
}
