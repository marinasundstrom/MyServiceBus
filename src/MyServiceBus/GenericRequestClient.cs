using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class GenericRequestClient<TRequest> : IRequestClient<TRequest>, IDisposable
    where TRequest : class
{
    private readonly ITransportFactory _transportFactory;
    private readonly IMessageSerializer _serializer;

    public GenericRequestClient(ITransportFactory transportFactory, IMessageSerializer serializer)
    {
        this._transportFactory = transportFactory;
        _serializer = serializer;
    }

    public void Dispose()
    {

    }

    [Throws(typeof(UriFormatException), typeof(RequestFaultException), typeof(ArgumentOutOfRangeException))]
    public async Task<Response<T>> GetResponseAsync<T>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default) where T : class
    {
        var taskCompletionSource = new TaskCompletionSource<Response<T>>();

        var responseExchange = $"resp-{Guid.NewGuid():N}";
        var responseReceiveTopology = new ReceiveEndpointTopology
        {
            QueueName = responseExchange,
            ExchangeName = responseExchange,
            RoutingKey = "",
            ExchangeType = "fanout",
            Durable = false,
            AutoDelete = true
        };

        IReceiveTransport? responseReceiveTransport = null;

        var responseHandler = [Throws(typeof(ObjectDisposedException))] async (ReceiveContext context) =>
        {
            try
            {
                if (context.TryGetMessage<T>(out var responeMessage))
                {
                    var response = new Response<T>(responeMessage);
                    taskCompletionSource.TrySetResult(response);
                    return;
                }

                if (context.TryGetMessage<Fault<TRequest>>(out var fault))
                {
                    taskCompletionSource.TrySetException(new RequestFaultException("Send", fault!));
                }
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }
        };

        responseReceiveTransport = await _transportFactory.CreateReceiveTransport(responseReceiveTopology, responseHandler, cancellationToken);

        await responseReceiveTransport.Start(cancellationToken);

        var exchangeName = NamingConventions.GetExchangeName(request.GetType());

        var uri = new Uri($"rabbitmq://localhost/exchange/{exchangeName}");
        var requestSendTransport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var responseAddress = new Uri($"rabbitmq://localhost/exchange/{responseExchange}?durable=false&autodelete=true");
        var sendContext = new SendContext(MessageTypeCache.GetMessageTypes(typeof(TRequest)), _serializer, cancellationToken)
        {
            ResponseAddress = responseAddress,
            FaultAddress = responseAddress,
            MessageId = Guid.NewGuid().ToString()
        };

        contextCallback?.Invoke(sendContext);

        await requestSendTransport.Send(request, sendContext, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout.TimeSpan == default ? RequestTimeout.Default.TimeSpan : timeout.TimeSpan);

        await using var registration = timeoutCts.Token.Register([Throws(typeof(ObjectDisposedException))] () =>
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

    [Throws(typeof(UriFormatException), typeof(RequestFaultException), typeof(ArgumentOutOfRangeException))]
    public async Task<Response<T1, T2>> GetResponseAsync<T1, T2>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T1 : class
        where T2 : class
    {
        var taskCompletionSource = new TaskCompletionSource<Response<T1, T2>>();

        var responseExchange = $"resp-{Guid.NewGuid():N}";
        var responseReceiveTopology = new ReceiveEndpointTopology
        {
            QueueName = responseExchange,
            ExchangeName = responseExchange,
            RoutingKey = "",
            ExchangeType = "fanout",
            Durable = false,
            AutoDelete = true
        };

        IReceiveTransport? responseReceiveTransport = null;

        var responseHandler = [Throws(typeof(ObjectDisposedException))] async (ReceiveContext context) =>
        {
            try
            {
                if (context.TryGetMessage<T1>(out var message1))
                {
                    taskCompletionSource.TrySetResult(Response<T1, T2>.FromT1(message1));
                    return;
                }

                if (context.TryGetMessage<T2>(out var message2))
                {
                    taskCompletionSource.TrySetResult(Response<T1, T2>.FromT2(message2));
                    return;
                }

                if (!typeof(T1).IsAssignableFrom(typeof(Fault<TRequest>)) &&
                    !typeof(T2).IsAssignableFrom(typeof(Fault<TRequest>)) &&
                    context.TryGetMessage<Fault<TRequest>>(out var fault))
                {
                    taskCompletionSource.TrySetException(new RequestFaultException("Send", fault));
                }
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }
        };

        responseReceiveTransport = await _transportFactory.CreateReceiveTransport(responseReceiveTopology, responseHandler, cancellationToken);

        await responseReceiveTransport.Start(cancellationToken);

        var exchangeName = NamingConventions.GetExchangeName(request.GetType());

        var uri = new Uri($"rabbitmq://localhost/exchange/{exchangeName}");
        var requestSendTransport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var responseAddress = new Uri($"rabbitmq://localhost/exchange/{responseExchange}?durable=false&autodelete=true");
        var sendContext = new SendContext(MessageTypeCache.GetMessageTypes(typeof(TRequest)), _serializer, cancellationToken)
        {
            ResponseAddress = responseAddress,
            FaultAddress = responseAddress,
            MessageId = Guid.NewGuid().ToString()
        };

        contextCallback?.Invoke(sendContext);

        await requestSendTransport.Send(request, sendContext, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout.TimeSpan == default ? RequestTimeout.Default.TimeSpan : timeout.TimeSpan);

        await using var registration = timeoutCts.Token.Register([Throws(typeof(ObjectDisposedException))] () =>
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
