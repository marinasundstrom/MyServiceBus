using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class AzureServiceBusReceiveEndpointConfigurator
{
    private readonly string _queueName;
    private readonly Func<Type, string> _entityNameResolver;
    private readonly IList<Action<IMessageBus, IServiceProvider>> _endpointActions;
    private int? _retryCount;
    private TimeSpan? _retryDelay;
    private int? _prefetchCount;
    private Type? _serializerType;

    internal AzureServiceBusReceiveEndpointConfigurator(
        string queueName,
        Func<Type, string> entityNameResolver,
        IList<Action<IMessageBus, IServiceProvider>> endpointActions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        _queueName = queueName;
        _entityNameResolver = entityNameResolver;
        _endpointActions = endpointActions;
    }

    public void UseMessageRetry(Action<RetryConfigurator> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var retryConfigurator = new RetryConfigurator();
        configure(retryConfigurator);
        _retryCount = retryConfigurator.RetryCount;
        _retryDelay = retryConfigurator.Delay;
    }

    public void PrefetchCount(int prefetchCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(prefetchCount);
        _prefetchCount = prefetchCount;
    }

    public void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer
    {
        _serializerType = typeof(TSerializer);
    }

    public void ConfigureConsumer<T>(IBusRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
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
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}.", exception);
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
            binding.EntityName = _entityNameResolver(binding.MessageType);
        }

        consumer.PrefetchCount = _prefetchCount is null ? null : checked((ushort)_prefetchCount.Value);
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

    public void Consumer<TConsumer, TMessage>()
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        _endpointActions.Add((_, provider) =>
        {
            var registry = provider.GetRequiredService<TopologyRegistry>();
            registry.RegisterConsumer<TConsumer, TMessage>(
                _queueName,
                endpointNameIsExplicit: true,
                endpointNameFormatterType: null);
            var consumer = registry.Consumers[^1];
            ConfigureConsumer(new BusRegistrationContext(provider), consumer);
        });
    }

    public void Handler<T>(Func<ConsumeContext<T>, Task> handler) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        var entityName = _entityNameResolver(typeof(T));

        _endpointActions.Add((bus, provider) =>
        {
            var serializer = _serializerType is null
                ? null
                : (IMessageSerializer)ActivatorUtilities.CreateInstance(provider, _serializerType);
            bus.AddHandler(
                    _queueName,
                    entityName,
                    handler,
                    _retryCount,
                    _retryDelay,
                    _prefetchCount is null ? null : checked((ushort)_prefetchCount.Value),
                    serializer: serializer,
                    cancellationToken: CancellationToken.None)
                .GetAwaiter().GetResult();
        });
    }

}
