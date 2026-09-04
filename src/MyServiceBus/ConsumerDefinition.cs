namespace MyServiceBus;

/// <summary>
/// Describes consumer-specific policy before it is materialized into bus topology.
/// </summary>
public interface IConsumerDefinition
{
    Type ConsumerType { get; }

    string? EndpointName { get; }

    int? ConcurrentMessageLimit { get; }
}

/// <summary>
/// Defines the endpoint identity and execution policy associated with a consumer.
/// </summary>
/// <typeparam name="TConsumer">The consumer implementation type.</typeparam>
public class ConsumerDefinition<TConsumer> : IConsumerDefinition, IConsumerConfigurator<TConsumer>
    where TConsumer : class, IConsumer
{
    private string? endpointName;
    private int? concurrentMessageLimit;

    public Type ConsumerType => typeof(TConsumer);

    public string? EndpointName
    {
        get => endpointName;
        set
        {
            if (value is not null)
                ArgumentException.ThrowIfNullOrWhiteSpace(value);

            endpointName = value;
        }
    }

    public int? ConcurrentMessageLimit
    {
        get => concurrentMessageLimit;
        set
        {
            if (value is <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The concurrent message limit must be greater than zero.");

            concurrentMessageLimit = value;
        }
    }
}

internal sealed class ConsumerRegistrationConfigurator<TConsumer>(ConsumerDefinition<TConsumer> definition)
    : IConsumerRegistrationConfigurator<TConsumer>
    where TConsumer : class, IConsumer
{
    public Topology.ConsumerDefinitionModel Definition { get; } = new(
        definition.ConsumerType,
        definition.EndpointName,
        definition.ConcurrentMessageLimit);
}
