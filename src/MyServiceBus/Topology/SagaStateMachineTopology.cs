using System.Text.Json.Serialization;
using MyServiceBus.Orchestration;

namespace MyServiceBus.Topology;

/// <summary>
/// Describes a registered saga state machine and its receive endpoint.
/// </summary>
public sealed record SagaStateMachineTopology(
    [property: JsonPropertyName("definition")] SagaStateMachineDefinition Definition,
    [property: JsonPropertyName("endpointName")] string EndpointName);
