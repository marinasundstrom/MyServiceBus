using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public class MessageBus : IMessageBus, IReceiveEndpointConnector
{
    public static IBusFactory Factory { get; } = new DefaultBusFactory();

    private readonly ITransportFactory _transportFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISendPipe _sendPipe;
    private readonly IPublishPipe _publishPipe;
    private readonly IMessageSerializer _messageSerializer;
    private readonly Uri _address;
    private readonly IBusTopology _topology;
    private readonly ILogger<MessageBus>? _logger;
    private readonly ISendContextFactory _sendContextFactory;
    private readonly IPublishContextFactory _publishContextFactory;

    private readonly List<IReceiveTransport> _activeTransports = new();

    // Key = queue name, Value = list of registrations for that queue
    private readonly Dictionary<string, List<(string MessageUrn, Type MessageType, IConsumePipe Pipe, IMessageSerializer Serializer)>> _consumers = new();
    private readonly HashSet<Type> _consumerTypes = new();

    public MessageBus(ITransportFactory transportFactory, IServiceProvider serviceProvider,
        ISendPipe sendPipe, IPublishPipe publishPipe, IMessageSerializer messageSerializer, Uri address,
        ISendContextFactory sendContextFactory, IPublishContextFactory publishContextFactory)
    {
        _transportFactory = transportFactory;
        _serviceProvider = serviceProvider;
        _sendPipe = sendPipe;
        _publishPipe = publishPipe;
        _messageSerializer = messageSerializer;
        _topology = _serviceProvider.GetService<TopologyRegistry>() ?? new TopologyRegistry();
        _address = address;
        _logger = _serviceProvider.GetService<ILogger<MessageBus>>();
        _sendContextFactory = sendContextFactory;
        _publishContextFactory = publishContextFactory;
    }

    public Uri Address => _address;

    public IBusTopology Topology => _topology;

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public Task Publish<TMessage>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
    {
        var typed = message as TMessage ?? MessageProxy.Create<TMessage>(message);
        return Publish(typed, contextCallback, cancellationToken);
    }

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public async Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        var exchangeName = EntityNameFormatter.Format(message.GetType());

        var uri = new Uri(_address, $"exchange/{exchangeName}");
        _logger?.LogDebug("Publishing {MessageType} to {DestinationAddress}", typeof(T).Name, uri);
        var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = _publishContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), _messageSerializer, cancellationToken);
        context.RoutingKey = exchangeName;
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = _address;
        context.DestinationAddress = uri;

        contextCallback?.Invoke(context);

        await _publishPipe.Send(context);
        await _sendPipe.Send(context);
        await transport.Send(message, context, cancellationToken);
    }

    public IPublishEndpoint GetPublishEndpoint() => this;

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<TransportSendEndpoint>();
        ISendEndpoint endpoint = new TransportSendEndpoint(_transportFactory, _sendPipe, _messageSerializer, uri, _address, _sendContextFactory, logger);
        return Task.FromResult(endpoint);
    }

    [Throws(typeof(InvalidOperationException))]
    public async Task AddHandler<TMessage>(string queueName, string exchangeName, Func<ConsumeContext<TMessage>, Task> handler,
        int? retryCount = null, TimeSpan? retryDelay = null, ushort? prefetchCount = null,
        IDictionary<string, object?>? queueArguments = null, IMessageSerializer? serializer = null, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        var topology = new ReceiveEndpointTopology
        {
            QueueName = queueName,
            ExchangeName = exchangeName,
            RoutingKey = string.Empty,
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false,
            PrefetchCount = prefetchCount ?? 0,
            QueueArguments = queueArguments
        };

        var configurator = new PipeConfigurator<ConsumeContext<TMessage>>();
        configurator.UseFilter(new OpenTelemetryConsumeFilter<TMessage>());
        var errorLogger = _serviceProvider.GetService<ILogger<ErrorTransportFilter<TMessage>>>();
        configurator.UseFilter(new ErrorTransportFilter<TMessage>(errorLogger));
        configurator.UseFilter(new HandlerFaultFilter<TMessage>(_serviceProvider));
        if (retryCount.HasValue)
            configurator.UseRetry(retryCount.Value, retryDelay);
        configurator.UseFilter(new HandlerMessageFilter<TMessage>(handler));
        var pipe = new ConsumePipe<TMessage>(configurator.Build(_serviceProvider));

        [Throws(typeof(InvalidCastException))]
        async Task TransportHandler(ReceiveContext context)
        {
            var messageSerializer = serializer ?? _messageSerializer;
            var consumeContext = new ConsumeContextImpl<TMessage>(context, _transportFactory, _sendPipe, _publishPipe, messageSerializer, _address, _sendContextFactory, _publishContextFactory, _serviceProvider.GetService<ILoggerFactory>());
            await pipe.Send(consumeContext).ConfigureAwait(false);
        }

        var expectedUrn = MessageUrn.For(typeof(TMessage));
        Func<string?, bool> isRegistered = mt => mt == expectedUrn;
        var receiveTransport = await _transportFactory.CreateReceiveTransport(topology, TransportHandler, isRegistered, cancellationToken);
        _activeTransports.Add(receiveTransport);
    }

    [Throws(typeof(InvalidOperationException), typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public async Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        var messageType = consumer.Bindings.First().MessageType;
        var messageUrn = MessageUrn.For(messageType);
        var queueName = consumer.QueueName;
        if (_consumerTypes.Contains(typeof(TConsumer)))
        {
            _logger?.LogDebug("Consumer {ConsumerType} already registered, skipping", typeof(TConsumer).Name);
            return;
        }

        var topology = new ReceiveEndpointTopology
        {
            QueueName = queueName,
            ExchangeName = EntityNameFormatter.Format(messageType)!, // standard MT routing
            RoutingKey = "", // messageType.FullName!,
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false,
            PrefetchCount = consumer.PrefetchCount ?? 0,
            QueueArguments = consumer.QueueArguments
        };

        Func<string?, bool> isRegistered = mt =>
            _consumers.TryGetValue(queueName, out var regs) && regs.Any(r => r.MessageUrn == mt);
        var receiveTransport = await _transportFactory.CreateReceiveTransport(
            topology,
            [Throws(typeof(TargetInvocationException), typeof(InvalidComObjectException), typeof(COMException), typeof(TypeLoadException))] (ctx) => HandleMessageAsync(queueName, ctx),
            isRegistered,
            cancellationToken);

        var configurator = new PipeConfigurator<ConsumeContext<TMessage>>();
        configurator.UseFilter(new OpenTelemetryConsumeFilter<TMessage>());
        var errorLogger = _serviceProvider.GetService<ILogger<ErrorTransportFilter<TMessage>>>();
        configurator.UseFilter(new ErrorTransportFilter<TMessage>(errorLogger));
        configurator.UseFilter(new ConsumerFaultFilter<TConsumer, TMessage>(_serviceProvider));
        if (configure is Action<PipeConfigurator<ConsumeContext<TMessage>>> cfg)
            cfg(configurator);
        configurator.UseFilter(new ConsumerMessageFilter<TConsumer, TMessage>(_serviceProvider));
        var pipe = new ConsumePipe<TMessage>(configurator.Build(_serviceProvider));

        var serializer = consumer.SerializerType != null
            ? (IMessageSerializer)ActivatorUtilities.CreateInstance(_serviceProvider, consumer.SerializerType)
            : _messageSerializer;

        if (!_consumers.TryGetValue(queueName, out var registrations))
            registrations = _consumers[queueName] = new List<(string, Type, IConsumePipe, IMessageSerializer)>();
        registrations.Add((messageUrn, messageType, pipe, serializer));
        _consumerTypes.Add(typeof(TConsumer));
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

    [Throws(typeof(InvalidOperationException), typeof(NotSupportedException), typeof(InvalidCastException), typeof(TargetInvocationException), typeof(MemberAccessException), typeof(InvalidComObjectException), typeof(COMException), typeof(TypeLoadException))]
    private async Task HandleMessageAsync(string queueName, ReceiveContext context)
    {
        var messageTypeName = context.MessageType.FirstOrDefault();
        if (messageTypeName == null || !_consumers.TryGetValue(queueName, out var registrations))
        {
            _logger?.LogWarning("Received message with unregistered type {MessageType}", messageTypeName ?? "<null>");
            return;
        }

        var matches = registrations.Where(r => r.MessageUrn == messageTypeName).ToList();
        if (matches.Count == 0)
        {
            _logger?.LogWarning("Received message with unregistered type {MessageType}", messageTypeName);
            return;
        }

        foreach (var registration in matches)
        {
            var consumeContextType = typeof(ConsumeContextImpl<>).MakeGenericType(registration.MessageType);
            var consumeContext = (ConsumeContext)Activator.CreateInstance(
                consumeContextType,
                context,
                _transportFactory,
                _sendPipe,
                _publishPipe,
                registration.Serializer,
                _address,
                _sendContextFactory,
                _publishContextFactory,
                _serviceProvider.GetService<ILoggerFactory>())
                ?? throw new InvalidOperationException("Failed to create ConsumeContext");

            _logger?.LogDebug("Received {MessageType}", messageTypeName);
            await registration.Pipe.Send(consumeContext);
        }
    }

}
