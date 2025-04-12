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

    public async Task<Response<T>> GetResponseAsync<T>(TRequest request, CancellationToken cancellationToken = default) where T : class
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

        IReceiveTransport? requestReceiveTransport = null;

        var handler = async (ReceiveContext context) =>
        {
            if (context.TryGetMessage<T>(out var message))
            {
                taskCompletionSource.SetResult(new Response<T>(message));

                await requestReceiveTransport.Stop(cancellationToken);
            }
        };

        requestReceiveTransport = await _transportFactory.CreateReceiveTransport(responseReceiveTopology, handler, cancellationToken);

        await requestReceiveTransport.Start(cancellationToken);

        var exchangeName = NamingConventions.GetExchangeName(request.GetType());

        var uri = new Uri($"rabbitmq://localhost/{exchangeName}");
        var requestSendTransport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = new SendContext([typeof(TRequest)], new EnvelopeMessageSerializer())
        {
            //RoutingKey = exchangeName,
            ResponseAddress = new Uri($"queue:{NamingConventions.GetQueueName(typeof(T))}"),
            MessageId = Guid.NewGuid().ToString()
        };

        await requestSendTransport.Send(request, context, cancellationToken);

        return await taskCompletionSource.Task;
    }
}