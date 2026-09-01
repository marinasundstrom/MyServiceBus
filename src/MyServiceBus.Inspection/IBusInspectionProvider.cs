using System.Collections.Generic;
using MyServiceBus.Choreography;

namespace MyServiceBus.Inspection;

public interface IBusInspectionProvider
{
    BusInspectionSnapshot GetSnapshot();
}

public sealed record BusInspectionSnapshot
{
    public string TransportName { get; init; }
    public Uri Address { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public IReadOnlyList<MessageInspection> Messages { get; init; }
    public IReadOnlyList<ReceiveEndpointInspection> ReceiveEndpoints { get; init; }
    public IReadOnlyList<ConsumerInspection> Consumers { get; init; }
    public IReadOnlyList<ChoreographyFragment> Choreographies { get; init; }

    public BusInspectionSnapshot(
        string transportName,
        Uri address,
        DateTimeOffset capturedAt,
        IReadOnlyList<MessageInspection> messages,
        IReadOnlyList<ReceiveEndpointInspection> receiveEndpoints,
        IReadOnlyList<ConsumerInspection> consumers,
        IReadOnlyList<ChoreographyFragment>? choreographies = null)
    {
        TransportName = transportName;
        Address = address;
        CapturedAt = capturedAt;
        Messages = messages;
        ReceiveEndpoints = receiveEndpoints;
        Consumers = consumers;
        Choreographies = choreographies ?? [];
    }
}

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
