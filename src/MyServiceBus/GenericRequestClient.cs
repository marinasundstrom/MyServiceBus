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

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(ArgumentOutOfRangeException))]
    public async Task<Response<T>> GetResponseAsync<T>(TRequest request, CancellationToken cancellationToken = default, RequestTimeout timeout = default) where T : class
    {
        var taskCompletionSource = new TaskCompletionSource<Response<T>>();

        var responseReceiveTopology = new ReceiveEndpointTopology
        {
            QueueName = $"{NamingConventions.GetQueueName(typeof(T))}",
            ExchangeName = NamingConventions.GetExchangeName(typeof(T))!, // standard MT routing
            RoutingKey = "", // messageType.FullName!,
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
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
                    var exceptionMessage = fault.Exceptions.FirstOrDefault()?.Message ?? "Request faulted";
                    taskCompletionSource.TrySetException(new InvalidOperationException(exceptionMessage));
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

        var sendContext = new SendContext(MessageTypeCache.GetMessageTypes(typeof(TRequest)), _serializer, cancellationToken)
        {
            ResponseAddress = new Uri($"rabbitmq://localhost/exchange/{NamingConventions.GetExchangeName(typeof(T))}"),
            MessageId = Guid.NewGuid().ToString()
        };

        await requestSendTransport.Send(request, sendContext, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout.TimeSpan == default ? RequestTimeout.Default.TimeSpan : timeout.TimeSpan);

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
