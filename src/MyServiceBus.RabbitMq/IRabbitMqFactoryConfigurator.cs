

using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IRabbitMqFactoryConfigurator
{
    void Message<T>(Action<MessageConfigurator> configure);
    string GetEntityName(Type messageType) => MyServiceBus.EntityNameFormatter.Format(messageType);
    void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure);
    void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null);
    void Host(string host, int port, Action<IRabbitMqHostConfigurator>? configure = null);
    void SetEndpointNameFormatter(IEndpointNameFormatter formatter);
    void SetEntityNameFormatter(IMessageEntityNameFormatter formatter);
    IEndpointNameFormatter? EndpointNameFormatter { get; }
    IMessageEntityNameFormatter? EntityNameFormatter { get; }
    string ClientHost { get; }
    int ClientPort { get; }
    ushort PrefetchCount { get; }
    void SetPrefetchCount(ushort prefetchCount);
    void SetConsumerFactory(Type consumerFactoryType);
}

public interface IRabbitMqHostConfigurator
{
    void Username(string username);
    void Password(string password);
}

public class MessageConfigurator
{
    private readonly Type _messageType;
    private readonly IDictionary<Type, string> _exchangeNames;

    public MessageConfigurator(Type messageType, IDictionary<Type, string> exchangeNames)
    {
        _messageType = messageType;
        _exchangeNames = exchangeNames;
    }

    public void SetEntityName(string name)
    {
        _exchangeNames[_messageType] = name;
    }

    public void SetEntityNameFormatter<T>(IMessageEntityNameFormatter<T> formatter)
    {
        _exchangeNames[_messageType] = formatter.FormatEntityName();
    }
}

public class ReceiveEndpointConfigurator
{
    private readonly string _queueName;
    private readonly IDictionary<Type, string> _exchangeNames;
    private readonly IList<Action<IMessageBus, IServiceProvider>> _endpointActions;
    private int? _retryCount;
    private TimeSpan? _retryDelay;
    private ushort? _prefetchCount;
    private IDictionary<string, object?>? _queueArguments;
    private Type? _serializerType;

    public ReceiveEndpointConfigurator(string queueName, IDictionary<Type, string> exchangeNames, IList<Action<IMessageBus, IServiceProvider>> endpointActions)
    {
        _queueName = queueName;
        _exchangeNames = exchangeNames;
        _endpointActions = endpointActions;
    }

    public void UseMessageRetry(Action<RetryConfigurator> configure)
    {
        var rc = new RetryConfigurator();
        configure(rc);
        _retryCount = rc.RetryCount;
        _retryDelay = rc.Delay;
    }

    public void SetQueueArgument(string key, object value)
    {
        _queueArguments ??= new Dictionary<string, object?>();
        _queueArguments[key] = value;
    }

    public void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer
    {
        _serializerType = typeof(TSerializer);
    }

    public void ConfigureConsumer<T>(IBusRegistrationContext context)
    {
        var consumerType = typeof(T);

        try
        {
            var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
            var consumers = registry.Consumers.Where(c => c.ConsumerType == consumerType).ToArray();
            if (consumers.Length == 0)
                throw new InvalidOperationException($"Consumer {consumerType.Name} is not registered.");
            foreach (var consumer in consumers)
                ConfigureConsumer(context, consumer);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}", ex);
        }
    }

    internal void ConfigureConsumer(IBusRegistrationContext context, ConsumerTopology consumer)
    {
        var registration = consumer.Registration
            ?? throw new InvalidOperationException($"Consumer {consumer.ConsumerType} has no runtime registration descriptor.");
        var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
        registry.MoveConsumerToEndpoint(consumer, _queueName);

        foreach (var binding in consumer.Bindings)
        {
            if (_exchangeNames.TryGetValue(binding.MessageType, out var entity))
                binding.EntityName = entity;
        }

        consumer.PrefetchCount = _prefetchCount;
        consumer.QueueArguments = _queueArguments;
        consumer.SerializerType = _serializerType;

        if (_retryCount.HasValue)
        {
            var retryConfiguration = registration.CreateRetryConfiguration(_retryCount.Value, _retryDelay);
            consumer.ConfigurePipe = consumer.ConfigurePipe is null
                ? retryConfiguration
                : Delegate.Combine(retryConfiguration, consumer.ConfigurePipe);
        }

        var bus = context.ServiceProvider.GetRequiredService<IMessageBus>();
        registration.Register(bus, consumer, CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Handler<T>(Func<ConsumeContext<T>, Task> handler)
        where T : class
    {
        var exchangeName = _exchangeNames.TryGetValue(typeof(T), out var entity)
            ? entity
            : EntityNameFormatter.Format(typeof(T))!;
        _endpointActions.Add((bus, provider) =>
        {
            IMessageSerializer? serializer = _serializerType != null
                ? (IMessageSerializer)ActivatorUtilities.CreateInstance(provider, _serializerType)
                : null;
            bus.AddHandler(_queueName, exchangeName, handler, _retryCount, _retryDelay, _prefetchCount, _queueArguments, serializer, CancellationToken.None).GetAwaiter().GetResult();
        });
    }

    public void PrefetchCount(ushort prefetchCount)
    {
        _prefetchCount = prefetchCount;
    }

}
