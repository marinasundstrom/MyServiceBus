namespace MyServiceBus;

public sealed record TransportCapabilityRequirement(string Capability, bool RequireNative);

public sealed class TransportCapabilityRequirements
{
    private readonly List<TransportCapabilityRequirement> _items = [];

    public IReadOnlyList<TransportCapabilityRequirement> Items => _items;

    public void Require(string capability, bool requireNative = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        _items.Add(new TransportCapabilityRequirement(capability, requireNative));
    }
}

public sealed class UnsupportedTransportCapabilityException : NotSupportedException
{
    public UnsupportedTransportCapabilityException(
        string transport,
        string capability,
        TransportCapabilitySupport actualSupport,
        bool requireNative)
        : base(CreateMessage(transport, capability, actualSupport, requireNative))
    {
        Transport = transport;
        Capability = capability;
        ActualSupport = actualSupport;
        RequireNative = requireNative;
    }

    public string Transport { get; }
    public string Capability { get; }
    public TransportCapabilitySupport ActualSupport { get; }
    public bool RequireNative { get; }

    private static string CreateMessage(
        string transport,
        string capability,
        TransportCapabilitySupport actualSupport,
        bool requireNative) =>
        $"Transport '{transport}' does not satisfy required capability '{capability}': " +
        $"required '{(requireNative ? "native" : "available")}', actual '{actualSupport.ToString().ToLowerInvariant()}'.";
}

public static class TransportCapabilityValidator
{
    public static void Validate(
        TransportCapabilityDescriptor descriptor,
        IEnumerable<TransportCapabilityRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            var actual = descriptor.Get(requirement.Capability);
            if (actual == TransportCapabilitySupport.Unsupported ||
                requirement.RequireNative && actual != TransportCapabilitySupport.Native)
            {
                throw new UnsupportedTransportCapabilityException(
                    descriptor.Transport,
                    requirement.Capability,
                    actual,
                    requirement.RequireNative);
            }
        }
    }
}
