namespace MyServiceBus.Topology;

public sealed class ReceiveEndpointTransportTopology
{
    public ReceiveEndpointTransportTopology(
        string name,
        bool durable,
        bool temporary,
        ushort prefetchCount,
        IReadOnlyList<MessageBinding> bindings,
        IReadOnlyDictionary<string, object?>? transportOptions = null,
        int concurrentMessageLimit = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bindings);

        if (durable && temporary)
            throw new ArgumentException("A receive endpoint cannot be both durable and temporary.");

        if (bindings.Count == 0)
            throw new ArgumentException("A receive endpoint must have at least one binding.", nameof(bindings));

        ArgumentOutOfRangeException.ThrowIfLessThan(concurrentMessageLimit, 1);

        Name = name;
        Durable = durable;
        Temporary = temporary;
        PrefetchCount = prefetchCount;
        ConcurrentMessageLimit = concurrentMessageLimit;
        Bindings = bindings
            .Select(binding => new MessageBinding
            {
                MessageType = binding.MessageType,
                EntityName = binding.EntityName
            })
            .ToArray();
        TransportOptions = transportOptions is null
            ? null
            : new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(transportOptions, StringComparer.Ordinal));
    }

    public string Name { get; }
    public bool Durable { get; }
    public bool Temporary { get; }
    public ushort PrefetchCount { get; }
    public int ConcurrentMessageLimit { get; }
    public IReadOnlyList<MessageBinding> Bindings { get; }
    public IReadOnlyDictionary<string, object?>? TransportOptions { get; }
}
