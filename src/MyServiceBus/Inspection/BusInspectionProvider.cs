using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Topology;

namespace MyServiceBus.Inspection;

public sealed class BusInspectionProvider : IBusInspectionProvider
{
    private readonly IMessageBus _bus;

    public BusInspectionProvider(IMessageBus bus)
    {
        _bus = bus;
    }

    public BusInspectionSnapshot GetSnapshot()
    {
        var transportName = _bus.Address.Scheme;
        var messages = _bus.Topology.Messages
            .OrderBy(x => x.EntityName, StringComparer.Ordinal)
            .ThenBy(x => FormatTypeName(x.MessageType), StringComparer.Ordinal)
            .Select(x => new MessageInspection(
                FormatTypeName(x.MessageType),
                MessageUrn.For(x.MessageType),
                x.EntityName,
                x.ImplementedInterfaces.Select(FormatTypeName).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                EmptyProperties()))
            .ToArray();

        var consumers = _bus.Topology.Consumers
            .OrderBy(x => x.QueueName, StringComparer.Ordinal)
            .ThenBy(x => FormatTypeName(x.ConsumerType), StringComparer.Ordinal)
            .Select(x => new ConsumerInspection(
                FormatTypeName(x.ConsumerType),
                x.QueueName,
                x.PrefetchCount,
                x.SerializerType is null ? null : FormatTypeName(x.SerializerType),
                CloneProperties(x.QueueArguments)))
            .ToArray();

        var endpoints = _bus.Topology.Consumers
            .GroupBy(x => x.QueueName, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var endpointAddress = new Uri(_bus.Address, group.Key).ToString();
                var transport = BuildTransportDetails(transportName, group.Key, first);

                return new ReceiveEndpointInspection(
                    group.Key,
                    endpointAddress,
                    group.SelectMany(x => x.Bindings)
                        .GroupBy(x => $"{FormatTypeName(x.MessageType)}|{x.EntityName}", StringComparer.Ordinal)
                        .Select(x => x.First())
                        .OrderBy(x => x.EntityName, StringComparer.Ordinal)
                        .ThenBy(x => FormatTypeName(x.MessageType), StringComparer.Ordinal)
                        .Select(x => new MessageBindingInspection(
                            FormatTypeName(x.MessageType),
                            MessageUrn.For(x.MessageType),
                            x.EntityName,
                            EmptyProperties()))
                        .ToArray(),
                    group.Select(x => FormatTypeName(x.ConsumerType))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToArray(),
                    transport,
                    CloneProperties(first.QueueArguments));
            })
            .ToArray();

        return new BusInspectionSnapshot(
            transportName,
            _bus.Address,
            DateTimeOffset.UtcNow,
            messages,
            endpoints,
            consumers);
    }

    private static TransportInspectionDetails? BuildTransportDetails(string transportName, string endpointName, ConsumerTopology consumer)
    {
        if (!string.Equals(transportName, "rabbitmq", StringComparison.OrdinalIgnoreCase))
            return null;

        return new TransportInspectionDetails(
            "rabbitmq",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["queueName"] = endpointName,
                ["exchangeName"] = consumer.Bindings.FirstOrDefault()?.EntityName ?? endpointName,
                ["exchangeType"] = "fanout",
                ["routingKey"] = string.Empty,
                ["durable"] = true,
                ["autoDelete"] = false,
                ["errorQueueName"] = $"{endpointName}_error",
                ["faultQueueName"] = $"{endpointName}_fault",
                ["skippedQueueName"] = $"{endpointName}_skipped"
            });
    }

    private static IReadOnlyDictionary<string, object?> CloneProperties(IDictionary<string, object?>? values)
        => values is null
            ? EmptyProperties()
            : new Dictionary<string, object?>(values, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, object?> EmptyProperties()
        => new Dictionary<string, object?>(StringComparer.Ordinal);

    private static string FormatTypeName(Type type)
        => type.FullName ?? type.Name;
}
