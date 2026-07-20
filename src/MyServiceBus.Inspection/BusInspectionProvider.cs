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
        var topology = _bus.Topology.GetSnapshot();
        var messagesById = topology.Messages.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var consumersById = topology.Consumers.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var bindingsById = topology.Bindings.ToDictionary(x => x.Id, StringComparer.Ordinal);

        var messages = topology.Messages
            .Select(x => new MessageInspection(
                x.Type,
                x.MessageUrn,
                x.EntityName,
                x.ImplementedMessageUrns,
                EmptyProperties()))
            .ToArray();

        var consumers = topology.Consumers
            .Select(x => new ConsumerInspection(
                x.Type,
                topology.ReceiveEndpoints.Single(endpoint => endpoint.Id == x.EndpointId).Name,
                null,
                null,
                EmptyProperties()))
            .ToArray();

        var endpoints = topology.ReceiveEndpoints
            .Select(endpoint => new ReceiveEndpointInspection(
                endpoint.Name,
                endpoint.LogicalAddress,
                endpoint.BindingIds.Select(id => bindingsById[id])
                    .Select(binding =>
                    {
                        var message = messagesById[binding.MessageId];
                        return new MessageBindingInspection(
                            message.Type,
                            message.MessageUrn,
                            binding.EntityName,
                            EmptyProperties());
                    })
                    .ToArray(),
                endpoint.ConsumerIds.Select(id => consumersById[id].Type).ToArray(),
                null,
                EmptyProperties()))
            .ToArray();

        return new BusInspectionSnapshot(
            _bus.Address.Scheme,
            _bus.Address,
            DateTimeOffset.UtcNow,
            messages,
            endpoints,
            consumers);
    }

    private static IReadOnlyDictionary<string, object?> EmptyProperties()
        => new Dictionary<string, object?>(StringComparer.Ordinal);
}
