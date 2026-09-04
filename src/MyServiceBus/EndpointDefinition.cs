namespace MyServiceBus;

/// <summary>
/// Describes transport-neutral policy for the endpoint that hosts one or more consumers.
/// </summary>
public sealed class EndpointDefinition
{
    private string? name;
    private int? concurrentMessageLimit;
    private ushort? prefetchCount;

    public string? Name
    {
        get => name;
        set
        {
            if (value is not null)
                ArgumentException.ThrowIfNullOrWhiteSpace(value);

            name = value;
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

    public ushort? PrefetchCount
    {
        get => prefetchCount;
        set
        {
            if (value is 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The prefetch count must be greater than zero.");

            prefetchCount = value;
        }
    }
}
