using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

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

    public async Task Publish<T>(T message, CancellationToken cancellationToken = default)
       where T : class
    {
        var exchangeName = NamingConventions.GetExchangeName(message.GetType());

        var uri = new Uri($"rabbitmq://localhost/{exchangeName}");
        var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = new SendContext(new EnvelopeMessageSerializer())
        {
            RoutingKey = exchangeName,
            MessageId = Guid.NewGuid().ToString()
        };

        context.Headers["message-type"] = typeof(T).FullName!;

        await transport.Send(message, context, cancellationToken);
    }

    public async Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, CancellationToken cancellationToken = default)
        where TConsumer : IConsumer<TMessage>
        where TMessage : class
    {
        var messageType = consumer.Bindings.First().MessageType;
        var consumerType = consumer.ConsumerType;

        var topology = new ReceiveEndpointTopology
        {
            QueueName = consumer.QueueName,
            ExchangeName = NamingConventions.GetExchangeName(messageType)!, // standard MT routing
            RoutingKey = "", // messageType.FullName!,
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
        };

        var handler = new DelegatingConsumer(HandleMessageAsync);

        var receiveTransport = await _transportFactory.CreateReceiveTransport(topology, handler, cancellationToken);

        _registeredConsumers.Add(NamingConventions.GetMessageUrn(messageType), consumerType);
        _activeTransports.Add(receiveTransport);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(_activeTransports.Select(async transport => await transport.Start(cancellationToken)));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(_activeTransports.Select(async transport => await transport.Stop(cancellationToken)));
    }

    private async Task HandleMessageAsync(ReceiveContext context)
    {
        var messageTypeName = context.MessageType.First();
        if (messageTypeName == null || !_registeredConsumers.TryGetValue(messageTypeName, out var consumerType))
            throw new InvalidOperationException($"No consumer registered for message type: {messageTypeName}");

        var messageType = consumerType.GetInterfaces().First().GetGenericArguments()[0];

        /* var messageType = Type.GetType(messageTypeName, false)
                          ?? throw new InvalidOperationException($"Cannot resolve message type: {messageTypeName}"); */

        using var scope = _serviceProvider.CreateScope();
        var consumer = (IConsumer)scope.ServiceProvider.GetRequiredService(consumerType);

        // Create ConsumeContext<T> dynamically and assign the message
        var consumeContextType = typeof(ConsumeContextImpl<>).MakeGenericType(messageType);
        var consumeContext = Activator.CreateInstance(consumeContextType, context)
                            ?? throw new InvalidOperationException("Failed to create ConsumeContext");

        // Find the Consume method on IConsumer<T>
        var consumeMethod = consumerType.GetMethod("Consume", new[] { consumeContextType })
                          ?? throw new InvalidOperationException($"Consumer does not implement Consume({consumeContextType.Name})");

        await (Task)consumeMethod.Invoke(consumer, [consumeContext])!;
    }
}
