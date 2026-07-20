using MyServiceBus.Topology;
using System.Text.Json;

namespace MyServiceBus.Tests;

public class TopologySnapshotTests
{
    [Fact]
    public void Reads_canonical_topology_fixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "topology-fixtures", "basic-topology.json");

        var snapshot = JsonSerializer.Deserialize<TopologySnapshot>(File.ReadAllBytes(path));

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.Version);
        Assert.Equal("urn:message:Contracts:OrderSubmitted", Assert.Single(snapshot.Messages).Id);
        Assert.Equal("queue:orders", Assert.Single(snapshot.ReceiveEndpoints).LogicalAddress);
        Assert.Equal("publish", Assert.Single(snapshot.Bindings).Kind);
    }

    [Fact]
    public void Creates_deterministic_read_only_topology_model()
    {
        var registry = new TopologyRegistry();
        registry.RegisterMessage<OrderSubmitted>("contracts-order-submitted");
        registry.RegisterConsumer<OrderConsumer>(
            "orders",
            configurePipe: null,
            typeof(OrderSubmitted));

        var snapshot = ((IBusTopology)registry).GetSnapshot();

        Assert.Equal(1, snapshot.Version);
        var message = Assert.Single(snapshot.Messages);
        Assert.Equal(MessageUrn.For(typeof(OrderSubmitted)), message.Id);
        Assert.Equal(typeof(OrderSubmitted).FullName, message.Type);
        Assert.Equal("contracts-order-submitted", message.EntityName);
        Assert.Equal(
            [MessageUrn.For(typeof(IOrderEvent))],
            message.ImplementedMessageUrns);

        var endpoint = Assert.Single(snapshot.ReceiveEndpoints);
        Assert.Equal("endpoint:orders", endpoint.Id);
        Assert.Equal("queue:orders", endpoint.LogicalAddress);
        Assert.True(endpoint.Durable);
        Assert.False(endpoint.Temporary);

        var consumer = Assert.Single(snapshot.Consumers);
        Assert.Equal(endpoint.Id, consumer.EndpointId);
        Assert.Equal([message.Id], consumer.MessageIds);

        var binding = Assert.Single(snapshot.Bindings);
        Assert.Equal(endpoint.Id, binding.EndpointId);
        Assert.Equal(message.Id, binding.MessageId);
        Assert.Equal("publish", binding.Kind);
        Assert.Equal([consumer.Id], endpoint.ConsumerIds);
        Assert.Equal([binding.Id], endpoint.BindingIds);
    }

    private interface IOrderEvent;

    private sealed class OrderSubmitted : IOrderEvent;

    private sealed class OrderConsumer;
}
