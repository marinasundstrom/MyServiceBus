using System.ComponentModel;

namespace MyServiceBus.Topology;

/// <summary>
/// Provides the strongly typed operations required to register a consumer at runtime.
/// </summary>
/// <remarks>
/// Applications normally obtain descriptors through consumer registration or generated
/// consumer catalogs instead of implementing this interface directly.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IConsumerRegistrationDescriptor
{
    Type ConsumerType { get; }

    Type MessageType { get; }

    Task Register(
        IMessageBus bus,
        ConsumerTopology consumer,
        CancellationToken cancellationToken = default);

    Delegate CreateRetryConfiguration(int retryCount, TimeSpan? retryDelay);
}

internal sealed class ConsumerRegistrationDescriptor<TConsumer, TMessage> : IConsumerRegistrationDescriptor
    where TConsumer : class, IConsumer<TMessage>
    where TMessage : class
{
    public Type ConsumerType => typeof(TConsumer);

    public Type MessageType => typeof(TMessage);

    public Task Register(
        IMessageBus bus,
        ConsumerTopology consumer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(consumer);
        return bus.AddConsumer<TMessage, TConsumer>(consumer, consumer.ConfigurePipe, cancellationToken);
    }

    public Delegate CreateRetryConfiguration(int retryCount, TimeSpan? retryDelay)
    {
        void Configure(PipeConfigurator<ConsumeContext<TMessage>> pipe) => pipe.UseRetry(retryCount, retryDelay);
        return (Action<PipeConfigurator<ConsumeContext<TMessage>>>)Configure;
    }
}
