namespace MyServiceBus.Topology;

public sealed record ReceiveEndpointDefinition(
    string Name,
    bool Durable,
    bool Temporary);
