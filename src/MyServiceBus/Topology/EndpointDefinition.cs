using System.Collections.Generic;

namespace MyServiceBus.Topology;

public class EndpointDefinition
{
    public string Address { get; init; } = default!;
    public ushort ConcurrencyLimit { get; init; } = 0;
    public bool ConfigureErrorEndpoint { get; init; } = true;
    public object? TransportSettings { get; init; }
}
