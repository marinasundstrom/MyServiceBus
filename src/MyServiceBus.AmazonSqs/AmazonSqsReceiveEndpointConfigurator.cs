using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class AmazonSqsReceiveEndpointConfigurator
{
    private readonly string _queueName;
    private readonly Func<Type, string> _entityNameResolver;
    private readonly IList<Action<IMessageBus, IServiceProvider>> _endpointActions;
    private int? _retryCount;
    private TimeSpan? _retryDelay;
    private int? _prefetchCount;
    private int? _concurrentMessageLimit;
    private Type? _serializerType;

    internal AmazonSqsReceiveEndpointConfigurator(
        string queueName,
        Func<Type, string> entityNameResolver,
        IList<Action<IMessageBus, IServiceProvider>> endpointActions)
    {
        AmazonSqsEntityName.Validate(queueName);
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
        ArgumentOutOfRangeException.ThrowIfLessThan(prefetchCount, 1);
        _prefetchCount = prefetchCount;
    }

    public void ConcurrentMessageLimit(int concurrentMessageLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrentMessageLimit, 1);
        _concurrentMessageLimit = concurrentMessageLimit;
    }

    public void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer =>
        _serializerType = typeof(TSerializer);

    public void ConfigureConsumer<T>(IBusRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var consumers = context.ServiceProvider.GetRequiredService<TopologyRegistry>()
            .Consumers.Where(c => c.ConsumerType == typeof(T)).ToArray();
        if (consumers.Length == 0)
            throw new InvalidOperationException($"Consumer {typeof(T).Name} is not registered.");
        foreach (var consumer in consumers)
            ConfigureConsumer(context, consumer);
    }

    internal void ConfigureConsumer(IBusRegistrationContext context, ConsumerTopology consumer)
    {
        var registration = consumer.Registration
            ?? throw new InvalidOperationException($"Consumer {consumer.ConsumerType} has no runtime registration descriptor.");
        var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
        registry.MoveConsumerToEndpoint(consumer, _queueName);
        foreach (var binding in consumer.Bindings)
            binding.EntityName = _entityNameResolver(binding.MessageType);

        if (_prefetchCount is not null)
            consumer.PrefetchCount = checked((ushort)_prefetchCount.Value);
        if (_concurrentMessageLimit is not null)
            consumer.ConcurrentMessageLimit = _concurrentMessageLimit;
        consumer.SerializerType = _serializerType;
        if (_retryCount.HasValue)
        {
            var retry = registration.CreateRetryConfiguration(_retryCount.Value, _retryDelay);
            consumer.ConfigurePipe = consumer.ConfigurePipe is null ? retry : Delegate.Combine(retry, consumer.ConfigurePipe);
        }

        registration.Register(context.ServiceProvider.GetRequiredService<IMessageBus>(), consumer, CancellationToken.None)
            .GetAwaiter().GetResult();
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
                configurePipe: null,
                endpointNameIsExplicit: true,
                endpointNameFormatterType: null);
            ConfigureConsumer(new BusRegistrationContext(provider), registry.Consumers[^1]);
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
                    _queueName, entityName, handler, _retryCount, _retryDelay,
                    _prefetchCount is null ? null : checked((ushort)_prefetchCount.Value),
                    serializer: serializer, cancellationToken: CancellationToken.None,
                    concurrentMessageLimit: _concurrentMessageLimit)
                .GetAwaiter().GetResult();
        });
    }
}
