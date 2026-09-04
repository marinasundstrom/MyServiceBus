namespace MyServiceBus.Topology;

/// <summary>
/// Immutable endpoint policy captured during registration normalization.
/// </summary>
public sealed record EndpointDefinitionModel
{
    public EndpointDefinitionModel(
        string name,
        bool nameIsExplicit,
        Type? nameFormatterType,
        int? concurrentMessageLimit,
        ushort? prefetchCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (concurrentMessageLimit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(concurrentMessageLimit));
        if (prefetchCount is 0)
            throw new ArgumentOutOfRangeException(nameof(prefetchCount));

        Name = name;
        NameIsExplicit = nameIsExplicit;
        NameFormatterType = nameFormatterType;
        ConcurrentMessageLimit = concurrentMessageLimit;
        PrefetchCount = prefetchCount;
    }

    public string Name { get; }

    public bool NameIsExplicit { get; }

    public Type? NameFormatterType { get; }

    public int? ConcurrentMessageLimit { get; }

    public ushort? PrefetchCount { get; }
}
