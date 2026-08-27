using System.Reflection;
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
            var messageType = consumerType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
                ?.GetGenericArguments().First();

            if (messageType is null)
                return;

            var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
            var consumer = registry.Consumers.First(c => c.ConsumerType == consumerType);
            registry.MoveConsumerToEndpoint(consumer, _queueName);
            foreach (var binding in consumer.Bindings)
            {
                binding.EntityName = _entityNameResolver(binding.MessageType);
            }

            consumer.PrefetchCount = _prefetchCount is null ? null : checked((ushort)_prefetchCount.Value);
            consumer.SerializerType = _serializerType;

            if (_retryCount.HasValue)
            {
                var retryMethod = typeof(AzureServiceBusReceiveEndpointConfigurator)
                    .GetMethod(nameof(ApplyRetry), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(messageType);
                var retryDelegate = (Delegate)retryMethod.Invoke(null, [_retryCount.Value, _retryDelay])!;
                consumer.ConfigurePipe = consumer.ConfigurePipe is not null
                    ? Delegate.Combine(retryDelegate, consumer.ConfigurePipe)
                    : retryDelegate;
            }

            var bus = context.ServiceProvider.GetRequiredService<IMessageBus>();
            var method = typeof(IMessageBus).GetMethod(nameof(IMessageBus.AddConsumer))!
                .MakeGenericMethod(messageType, consumerType);
            ((Task)method.Invoke(bus, [consumer, consumer.ConfigurePipe, CancellationToken.None])!)
                .GetAwaiter().GetResult();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"Failed to configure consumer {consumerType.Name}.", exception.InnerException);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}.", exception);
        }
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

    private static Delegate ApplyRetry<T>(int retryCount, TimeSpan? delay) where T : class
    {
        void Configure(PipeConfigurator<ConsumeContext<T>> pipe) => pipe.UseRetry(retryCount, delay);
        return (Action<PipeConfigurator<ConsumeContext<T>>>)Configure;
    }
}
