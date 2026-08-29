using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

internal sealed class Mediator : IMediator
{
    private readonly IMessageBus _bus;
    private readonly ITransportFactory _transportFactory;
    private readonly TopologyRegistry _topology;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageSerializer _serializer;
    private readonly ISendContextFactory _sendContextFactory;
    private readonly ISendPipe _sendPipe;

    public Mediator(
        IMessageBus bus,
        ITransportFactory transportFactory,
        TopologyRegistry topology,
        IServiceProvider serviceProvider,
        IMessageSerializer serializer,
        ISendContextFactory sendContextFactory,
        ISendPipe sendPipe)
    {
        _bus = bus;
        _transportFactory = transportFactory;
        _topology = topology;
        _serviceProvider = serviceProvider;
        _serializer = serializer;
        _sendContextFactory = sendContextFactory;
        _sendPipe = sendPipe;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : class
    {
        ArgumentNullException.ThrowIfNull(notification);
        return _bus.Publish(notification, cancellationToken: cancellationToken);
    }

    public async Task Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var messageType = request.GetType();
        var handler = GetSingleHandler(messageType);
        if (GetResultHandlerContracts(handler.ConsumerType, messageType).Any())
        {
            throw new MediatorResponseTypeException(messageType, typeof(void), handler.ConsumerType);
        }

        var address = _transportFactory.GetPublishAddress(messageType);
        var transport = await _transportFactory.GetSendTransport(address, cancellationToken).ConfigureAwait(false);
        var context = _sendContextFactory.Create(
            MessageTypeCache.GetMessageTypes(messageType),
            _serializer,
            cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = _bus.Address;
        context.DestinationAddress = address;

        await _sendPipe.Send(context).ConfigureAwait(false);
        await transport.Send(request, context, cancellationToken).ConfigureAwait(false);
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

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        var messageType = request.GetType();
        var handler = GetSingleHandler(messageType);
        var resultContracts = GetResultHandlerContracts(handler.ConsumerType, messageType).ToArray();
        if (resultContracts.Length > 0 && !resultContracts.Any(contract => typeof(TResponse).IsAssignableFrom(contract.ResponseType)))
            throw new MediatorResponseTypeException(messageType, typeof(TResponse), handler.ConsumerType);

        return SendRequest<TResponse>(request, messageType, cancellationToken);
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

    private async Task<TResponse> SendRequest<TResponse>(
        object request,
        Type requestType,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var completion = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var responseExchange = $"resp-{Guid.NewGuid():N}";
        var responseTopology = new ReceiveEndpointTransportTopology(
            responseExchange,
            durable: false,
            temporary: true,
            prefetchCount: 0,
            [new MessageBinding { MessageType = typeof(TResponse), EntityName = responseExchange }]);
        var requestId = Guid.NewGuid();

        var responseTransport = await _transportFactory.CreateReceiveTransport(
            responseTopology,
            context =>
            {
                if (context.RequestId != requestId)
                    return Task.CompletedTask;

                if (context.TryGetMessage<TResponse>(out var response))
                    completion.TrySetResult(response!);
                else if (context.TryGetMessage<Fault>(out var fault))
                    completion.TrySetException(new RequestFaultException(requestType.Name, fault!));

                return Task.CompletedTask;
            },
            null,
            cancellationToken).ConfigureAwait(false);

        await responseTransport.Start(cancellationToken).ConfigureAwait(false);
        try
        {
            var requestAddress = _transportFactory.GetPublishAddress(requestType);
            var requestTransport = await _transportFactory.GetSendTransport(requestAddress, cancellationToken).ConfigureAwait(false);
            var responseAddress = _transportFactory.GetTemporaryEndpointAddress(responseExchange);
            var sendContext = _sendContextFactory.Create(
                MessageTypeCache.GetMessageTypes(requestType),
                _serializer,
                cancellationToken);
            sendContext.ResponseAddress = responseAddress;
            sendContext.FaultAddress = responseAddress;
            sendContext.MessageId = Guid.NewGuid().ToString();
            sendContext.RequestId = requestId;

            await requestTransport.Send(request, sendContext, cancellationToken).ConfigureAwait(false);
            return await completion.Task
                .WaitAsync(RequestTimeout.Default.TimeSpan, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await responseTransport.Stop(CancellationToken.None).ConfigureAwait(false);
        }
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
