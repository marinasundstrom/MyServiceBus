using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

namespace MyServiceBus;

internal sealed class Mediator : IMediator
{
    private readonly IMessageBus _bus;
    private readonly ITransportFactory _transportFactory;
    private readonly TopologyRegistry _topology;
    private readonly IServiceProvider _serviceProvider;

    public Mediator(
        IMessageBus bus,
        ITransportFactory transportFactory,
        TopologyRegistry topology,
        IServiceProvider serviceProvider)
    {
        _bus = bus;
        _transportFactory = transportFactory;
        _topology = topology;
        _serviceProvider = serviceProvider;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : class
    {
        ArgumentNullException.ThrowIfNull(notification);
        return _bus.Publish(notification, cancellationToken: cancellationToken);
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);
        var messageType = request.GetType();
        var handler = GetSingleHandler(messageType);
        if (GetResultHandlerContracts(handler.ConsumerType, messageType).Any())
        {
            throw new MediatorResponseTypeException(messageType, typeof(void), handler.ConsumerType);
        }

        var endpoint = await _bus.GetSendEndpoint(_transportFactory.GetPublishAddress(request.GetType())).ConfigureAwait(false);
        await endpoint.Send(request, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        var messageType = request.GetType();
        var handler = GetSingleHandler(messageType);
        var resultContracts = GetResultHandlerContracts(handler.ConsumerType, messageType).ToArray();
        if (resultContracts.Length > 0 && !resultContracts.Any(contract => typeof(TResponse).IsAssignableFrom(contract.ResponseType)))
            throw new MediatorResponseTypeException(messageType, typeof(TResponse), handler.ConsumerType);

        var client = _serviceProvider.GetRequiredService<IRequestClient<TRequest>>();
        var response = await client.GetResponseAsync<TResponse>(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Message;
    }

    public IRequestClient<TRequest> CreateRequestClient<TRequest>()
        where TRequest : class
    {
        GetSingleHandler(typeof(TRequest));
        return _serviceProvider.GetRequiredService<IRequestClient<TRequest>>();
    }

    private ConsumerTopology GetSingleHandler(Type messageType)
    {
        var handlers = _topology.Consumers
            .Where(consumer => consumer.Bindings.Any(binding => binding.MessageType.IsAssignableFrom(messageType)))
            .ToArray();

        return handlers.Length switch
        {
            0 => throw new MediatorHandlerNotFoundException(messageType),
            1 => handlers[0],
            _ => throw new MediatorHandlerCardinalityException(
                messageType,
                handlers.Select(handler => handler.ConsumerType).ToArray())
        };
    }

    private static IEnumerable<(Type RequestType, Type ResponseType)> GetResultHandlerContracts(
        Type handlerType,
        Type messageType)
        => handlerType.GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IHandler<,>))
            .Select(type => type.GetGenericArguments())
            .Where(arguments => arguments[0].IsAssignableFrom(messageType))
            .Select(arguments => (arguments[0], arguments[1]));
}
