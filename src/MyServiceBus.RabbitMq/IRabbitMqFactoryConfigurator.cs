

using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IRabbitMqFactoryConfigurator
{
    void Message<T>(Action<MessageConfigurator> configure);
    void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure);
    void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null);
    void SetEndpointNameFormatter(IEndpointNameFormatter formatter);
    void SetEntityNameFormatter(IMessageEntityNameFormatter formatter);
    IEndpointNameFormatter? EndpointNameFormatter { get; }
    IMessageEntityNameFormatter? EntityNameFormatter { get; }
    string ClientHost { get; }
    ushort PrefetchCount { get; }
    void SetPrefetchCount(ushort prefetchCount);
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

    [Throws(typeof(NotSupportedException))]
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

    [Throws(typeof(InvalidOperationException))]
    public void ConfigureConsumer<T>(IBusRegistrationContext context)
    {
        var consumerType = typeof(T);

        try
        {
            var messageType = consumerType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
                ?.GetGenericArguments().First();

            if (messageType == null)
                return;

            var bus = context.ServiceProvider.GetRequiredService<IMessageBus>();
            var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
            var consumer = registry.Consumers.First(c => c.ConsumerType == consumerType);

            consumer.Address = _queueName;

            foreach (var binding in consumer.Bindings)
            {
                if (_exchangeNames.TryGetValue(binding.MessageType, out var entity))
                    binding.EntityName = entity;
            }

            consumer.ConcurrencyLimit = _prefetchCount;
            if (_queueArguments != null)
            {
                var settings = consumer.TransportSettings as RabbitMqEndpointSettings ?? new RabbitMqEndpointSettings();
                settings.QueueArguments = _queueArguments;
                consumer.TransportSettings = settings;
            }
            consumer.SerializerType = _serializerType;

            if (_retryCount.HasValue)
            {
                var retryMethod = typeof(ReceiveEndpointConfigurator)
                    .GetMethod(nameof(ApplyRetry), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(messageType);
                var retryDelegate = (Delegate)retryMethod.Invoke(null, new object[] { _retryCount.Value, _retryDelay })!;
                consumer.ConfigurePipe = consumer.ConfigurePipe != null
                    ? Delegate.Combine(retryDelegate, consumer.ConfigurePipe)
                    : retryDelegate;
            }

            var method = typeof(IMessageBus).GetMethod("AddConsumer")!
                .MakeGenericMethod(messageType, consumerType);

            ((Task)method.Invoke(bus, new object[] { consumer, consumer.ConfigurePipe, CancellationToken.None }))
                .GetAwaiter().GetResult();
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}", ex.InnerException);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}", ex);
        }
    }

    [Throws(typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public void Handler<T>(Func<ConsumeContext<T>, Task> handler)
        where T : class
    {
        var exchangeName = _exchangeNames.TryGetValue(typeof(T), out var entity)
            ? entity
            : EntityNameFormatter.Format(typeof(T))!;
        _endpointActions.Add([Throws(typeof(Exception))] (bus, provider) =>
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

    static Delegate ApplyRetry<T>(int retryCount, TimeSpan? delay)
        where T : class
    {
        void Configure(PipeConfigurator<ConsumeContext<T>> pipe) => pipe.UseRetry(retryCount, delay);
        return (Action<PipeConfigurator<ConsumeContext<T>>>)Configure;
    }
}