using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using System.Reflection;

namespace MyServiceBus;

public class MyMessageBus : IMessageBus
{
    private readonly ITransportFactory _transportFactory;
    private readonly IServiceProvider _serviceProvider;

    private readonly List<IReceiveTransport> _activeTransports = new();

    // Key = message type URN, Value = registration containing message type and pipeline
    private readonly Dictionary<string, (Type MessageType, IConsumePipe Pipe)> _consumers = new();

    public MyMessageBus(ITransportFactory transportFactory, IServiceProvider serviceProvider)
    {
        _transportFactory = transportFactory;
        _serviceProvider = serviceProvider;
    }

    [Throws(typeof(UriFormatException))]
    public async Task Publish<T>(T message, CancellationToken cancellationToken = default)
       where T : class
    {
        var exchangeName = NamingConventions.GetExchangeName(message.GetType());

        var uri = new Uri($"rabbitmq://localhost/{exchangeName}");
        var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(T)), new EnvelopeMessageSerializer(), cancellationToken)
        {
            RoutingKey = exchangeName,
            MessageId = Guid.NewGuid().ToString()
        };

        //context.Headers["message-type"] = typeof(T).FullName!;

        await transport.Send(message, context, cancellationToken);
    }

    [Throws(typeof(InvalidOperationException))]
    public async Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, CancellationToken cancellationToken = default)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        var messageType = consumer.Bindings.First().MessageType;

        var topology = new ReceiveEndpointTopology
        {
            QueueName = consumer.QueueName,
            ExchangeName = NamingConventions.GetExchangeName(messageType)!, // standard MT routing
            RoutingKey = "", // messageType.FullName!,
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
        };

        var receiveTransport = await _transportFactory.CreateReceiveTransport(topology, HandleMessageAsync, cancellationToken);

        var configurator = new PipeConfigurator<ConsumeContext<TMessage>>();
        configurator.UseRetry(3);
        configurator.UseFilter(new ConsumerMessageFilter<TConsumer, TMessage>(_serviceProvider));
        var pipe = new ConsumePipe<TMessage>(configurator.Build());

        _consumers.Add(NamingConventions.GetMessageUrn(messageType), (messageType, pipe));
        _activeTransports.Add(receiveTransport);
    }

    [Throws(typeof(ArgumentException))]
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(_activeTransports.Select(async transport => await transport.Start(cancellationToken)));
    }

    [Throws(typeof(ArgumentException))]
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(_activeTransports.Select(async transport => await transport.Stop(cancellationToken)));
    }

    [Throws(typeof(InvalidOperationException))]
    private async Task HandleMessageAsync(ReceiveContext context)
    {
        var messageTypeName = context.MessageType.First();
        if (messageTypeName == null || !_consumers.TryGetValue(messageTypeName, out var registration))
            return;

        var consumeContextType = typeof(ConsumeContextImpl<>).MakeGenericType(registration.MessageType);
        var consumeContext = (ConsumeContext)Activator.CreateInstance(consumeContextType, context, _transportFactory)
            ?? throw new InvalidOperationException("Failed to create ConsumeContext");

        await registration.Pipe.Send(consumeContext);
    }
}
