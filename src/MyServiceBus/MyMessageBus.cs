using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public class MyMessageBus : IMessageBus
{
    private readonly ITransportFactory _transportFactory;
    private readonly IServiceProvider _serviceProvider;

    private readonly List<IReceiveTransport> _activeTransports = new();

    // Key = message type full name, Value = consumer type (e.g. UserRegistered => UserRegisteredConsumer)
    private readonly Dictionary<string, Type> _registeredConsumers = new();

    public MyMessageBus(ITransportFactory transportFactory, IServiceProvider serviceProvider)
    {
        _transportFactory = transportFactory;
        _serviceProvider = serviceProvider;
    }

    public async Task Publish<T>(T message, string topic, CancellationToken cancellationToken = default)
       where T : class
    {
        var uri = new Uri($"rabbitmq://localhost/{topic}");
        var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = new SendContext
        {
            RoutingKey = topic,
            MessageId = Guid.NewGuid().ToString()
        };

        context.Headers["message-type"] = typeof(T).FullName!;

        await transport.Send(message, context, cancellationToken);
    }

    public async Task AddConsumer<TMessage, TConsumer>(string queue, CancellationToken cancellationToken = default)
        where TConsumer : IConsumer<TMessage>
        where TMessage : class
    {
        var messageType = typeof(TMessage);
        var consumerType = typeof(TConsumer);

        _registeredConsumers[messageType.FullName!] = consumerType;

        var topology = new ReceiveEndpointTopology
        {
            QueueName = queue,
            ExchangeName = NamingHelpers.GetExchangeName(messageType)!, // standard MT routing
            RoutingKey = messageType.FullName!,
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
        };

        var handler = new DelegatingConsumer(HandleMessageAsync);

        var receiveTransport = await _transportFactory.CreateReceiveTransport(topology, handler, cancellationToken);

        _activeTransports.Add(receiveTransport);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var transport in _activeTransports)
        {
            await transport.Start(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var transport in _activeTransports)
        {
            await transport.Stop(cancellationToken);
        }
    }

    private async Task HandleMessageAsync(ReceiveContext context)
    {
        var messageTypeName = context.MessageType;
        if (messageTypeName == null || !_registeredConsumers.TryGetValue(messageTypeName, out var consumerType))
            throw new InvalidOperationException($"No consumer registered for message type: {messageTypeName}");

        var messageType = Type.GetType(messageTypeName)
                          ?? throw new InvalidOperationException($"Cannot resolve message type: {messageTypeName}");

        var message = context.Deserialize(messageType);

        using var scope = _serviceProvider.CreateScope();
        var consumer = (IConsumer)scope.ServiceProvider.GetRequiredService(consumerType);

        // Create ConsumeContext<T> dynamically and assign the message
        var consumeContextType = typeof(ConsumeContextImpl<>).MakeGenericType(messageType);
        var consumeContext = Activator.CreateInstance(consumeContextType, message)
                            ?? throw new InvalidOperationException("Failed to create ConsumeContext");

        // Find the Consume method on IConsumer<T>
        var consumeMethod = consumerType.GetMethod("Consume", new[] { consumeContextType })
                          ?? throw new InvalidOperationException($"Consumer does not implement Consume({consumeContextType.Name})");

        await (Task)consumeMethod.Invoke(consumer, new[] { consumeContext })!;
    }
}
