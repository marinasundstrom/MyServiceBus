using System.Collections.Generic;

namespace MyServiceBus.Inspection;

public interface IBusInspectionProvider
{
    BusInspectionSnapshot GetSnapshot();
}

public sealed record BusInspectionSnapshot(
    string TransportName,
    Uri Address,
    DateTimeOffset CapturedAt,
    IReadOnlyList<MessageInspection> Messages,
    IReadOnlyList<ReceiveEndpointInspection> ReceiveEndpoints,
    IReadOnlyList<ConsumerInspection> Consumers);

public sealed record MessageInspection(
    string MessageType,
    string MessageUrn,
    string EntityName,
    IReadOnlyList<string> ImplementedMessageTypes,
    IReadOnlyDictionary<string, object?> Properties);

public sealed record MessageBindingInspection(
    string MessageType,
    string MessageUrn,
    string EntityName,
    IReadOnlyDictionary<string, object?> Properties);

public sealed record ConsumerInspection(
    string ConsumerType,
    string EndpointName,
    int? PrefetchCount,
    string? SerializerType,
    IReadOnlyDictionary<string, object?> Properties);

public sealed record ReceiveEndpointInspection(
    string EndpointName,
    string Address,
    IReadOnlyList<MessageBindingInspection> Bindings,
    IReadOnlyList<string> ConsumerTypes,
    TransportInspectionDetails? Transport,
    IReadOnlyDictionary<string, object?> Properties);

public sealed record TransportInspectionDetails(
    string TransportName,
    IReadOnlyDictionary<string, object?> Properties);
