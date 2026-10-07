namespace MyServiceBus;

/// <summary>
/// Describes consumer-specific policy before it is materialized into bus topology.
/// </summary>
public interface IConsumerDefinition
{
    Type ConsumerType { get; }

    EndpointDefinition Endpoint { get; }

    string? EndpointName { get; }

    int? ConcurrentMessageLimit { get; }

    Delegate? GetMessagePipeline(Type messageType) => null;
}

/// <summary>
/// Defines the endpoint identity and execution policy associated with a consumer.
/// </summary>
/// <typeparam name="TConsumer">The consumer implementation type.</typeparam>
public class ConsumerDefinition<TConsumer> : IConsumerDefinition, IConsumerConfigurator<TConsumer>
    where TConsumer : class, IConsumer
{
    private readonly Dictionary<Type, Delegate> pipelines = new();

    public void ConfigureMessage<TMessage>(Action<PipeConfigurator<ConsumeContext<TMessage>>> configure) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        pipelines[typeof(TMessage)] = pipelines.TryGetValue(typeof(TMessage), out var existing)
            ? Delegate.Combine(existing, configure) : configure;
    }

    public Delegate? GetMessagePipeline(Type messageType) => pipelines.GetValueOrDefault(messageType);

    public ConsumerDefinition()
        : this(new EndpointDefinition())
    {
    }

    public ConsumerDefinition(EndpointDefinition endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        Endpoint = endpoint;
    }

    public Type ConsumerType => typeof(TConsumer);

    public EndpointDefinition Endpoint { get; }

    public string? EndpointName
    {
        get => Endpoint.Name;
        set => Endpoint.Name = value;
    }

    public int? ConcurrentMessageLimit
    {
        get => Endpoint.ConcurrentMessageLimit;
        set => Endpoint.ConcurrentMessageLimit = value;
    }

    public ushort? PrefetchCount
    {
        get => Endpoint.PrefetchCount;
        set => Endpoint.PrefetchCount = value;
    }
}

internal sealed class ConsumerRegistrationConfigurator<TConsumer>(Topology.ConsumerDefinitionModel definition)
    : IConsumerRegistrationConfigurator<TConsumer>
    where TConsumer : class, IConsumer
{
    public Topology.ConsumerDefinitionModel Definition { get; } = definition;
}
