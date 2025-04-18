using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class GenericRequestClient<TRequest> : IRequestClient<TRequest>, IDisposable
    where TRequest : class
{
    private readonly ITransportFactory _transportFactory;

    public GenericRequestClient(ITransportFactory transportFactory)
    {
        this._transportFactory = transportFactory;
    }

    public void Dispose()
    {

    }

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

        var responseHandler = async (ReceiveContext context) =>
        {
            try
            {
                if (context.TryGetMessage<T>(out var responeMessage))
                {
                    var response = new Response<T>(responeMessage);
                    taskCompletionSource.TrySetResult(response);
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

        var uri = new Uri($"rabbitmq://localhost/{exchangeName}");
        var requestSendTransport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var sendContext = new SendContext([typeof(TRequest)], new EnvelopeMessageSerializer())
        {
            //RoutingKey = exchangeName,
            ResponseAddress = new Uri($"queue:{NamingConventions.GetQueueName(typeof(T))}"),
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
