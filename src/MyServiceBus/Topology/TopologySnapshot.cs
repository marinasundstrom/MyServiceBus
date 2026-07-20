using System.Text.Json.Serialization;

namespace MyServiceBus.Topology;

public sealed record TopologySnapshot(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("messages")] IReadOnlyList<MessageTopologySnapshot> Messages,
    [property: JsonPropertyName("receiveEndpoints")] IReadOnlyList<ReceiveEndpointTopologySnapshot> ReceiveEndpoints,
    [property: JsonPropertyName("consumers")] IReadOnlyList<ConsumerTopologySnapshot> Consumers,
    [property: JsonPropertyName("bindings")] IReadOnlyList<MessageBindingTopologySnapshot> Bindings);

public sealed record MessageTopologySnapshot(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("messageUrn")] string MessageUrn,
    [property: JsonPropertyName("entityName")] string EntityName,
    [property: JsonPropertyName("implementedMessageUrns")] IReadOnlyList<string> ImplementedMessageUrns);

public sealed record ReceiveEndpointTopologySnapshot(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logicalAddress")] string LogicalAddress,
    [property: JsonPropertyName("durable")] bool Durable,
    [property: JsonPropertyName("temporary")] bool Temporary,
    [property: JsonPropertyName("consumerIds")] IReadOnlyList<string> ConsumerIds,
    [property: JsonPropertyName("bindingIds")] IReadOnlyList<string> BindingIds);

public sealed record ConsumerTopologySnapshot(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("endpointId")] string EndpointId,
    [property: JsonPropertyName("messageIds")] IReadOnlyList<string> MessageIds);

public sealed record MessageBindingTopologySnapshot(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("endpointId")] string EndpointId,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("entityName")] string EntityName,
    [property: JsonPropertyName("kind")] string Kind);

internal static class TopologySnapshotBuilder
{
    public static TopologySnapshot Create(IBusTopology topology)
    {
        var messages = topology.Messages
            .GroupBy(x => MessageUrn.For(x.MessageType), StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(message => new MessageTopologySnapshot(
                MessageUrn.For(message.MessageType),
                FormatTypeName(message.MessageType),
                MessageUrn.For(message.MessageType),
                message.EntityName,
                message.MessageType.GetInterfaces()
                    .Select(MessageUrn.For)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();

        var consumers = topology.Consumers
            .Select(consumer =>
            {
                var endpointId = EndpointId(consumer.QueueName);
                var consumerType = FormatTypeName(consumer.ConsumerType);
                return new ConsumerTopologySnapshot(
                    $"{endpointId}|consumer:{consumerType}",
                    consumerType,
                    endpointId,
                    consumer.Bindings.Select(x => MessageUrn.For(x.MessageType))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToArray());
            })
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();

        var bindings = topology.Consumers
            .SelectMany(consumer => consumer.Bindings.Select(binding =>
            {
                var endpointId = EndpointId(consumer.QueueName);
                var messageId = MessageUrn.For(binding.MessageType);
                return new MessageBindingTopologySnapshot(
                    $"{endpointId}|binding:{messageId}|{binding.EntityName}",
                    endpointId,
                    messageId,
                    binding.EntityName,
                    "publish");
            }))
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();

        var endpoints = topology.ReceiveEndpoints
            .Select(endpoint =>
            {
                var endpointId = EndpointId(endpoint.Name);
                return new ReceiveEndpointTopologySnapshot(
                    endpointId,
                    endpoint.Name,
                    $"queue:{endpoint.Name}",
                    endpoint.Durable,
                    endpoint.Temporary,
                    consumers.Where(x => x.EndpointId == endpointId).Select(x => x.Id).ToArray(),
                    bindings.Where(x => x.EndpointId == endpointId).Select(x => x.Id).ToArray());
            })
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();

        return new TopologySnapshot(1, messages, endpoints, consumers, bindings);
    }

    private static string EndpointId(string endpointName) => $"endpoint:{endpointName}";

    private static string FormatTypeName(Type type) => type.FullName ?? type.Name;
}
