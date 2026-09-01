using MyServiceBus.Topology;
using MyServiceBus.Choreography;
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
        Assert.Equal(TopologySnapshot.CurrentVersion, snapshot.Version);
        Assert.Equal("urn:message:Contracts:OrderSubmitted", Assert.Single(snapshot.Messages).Id);
        Assert.Equal("queue:orders", Assert.Single(snapshot.ReceiveEndpoints).LogicalAddress);
        Assert.Equal("publish", Assert.Single(snapshot.Bindings).Kind);
        Assert.Equal("order-fulfillment", Assert.Single(snapshot.Choreographies).ChoreographyId);
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
        registry.RegisterChoreography(new ChoreographyBuilder("order-fulfillment", "1", "orders")
            .Step<OrderSubmitted>("accept-order", step => step
                .OwnedBy<OrderConsumer>()
                .Publishes<OrderAccepted>(output => output.Exactly(1).Within(TimeSpan.FromSeconds(5))))
            .Build());

        var snapshot = ((IBusTopology)registry).GetSnapshot();

        Assert.Equal(TopologySnapshot.CurrentVersion, snapshot.Version);
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

        var choreography = Assert.Single(snapshot.Choreographies);
        Assert.Equal("order-fulfillment", choreography.ChoreographyId);
        Assert.Equal("orders", choreography.Owner);
        Assert.Equal(MessageUrn.For(typeof(OrderSubmitted)), Assert.Single(choreography.Steps).TriggerMessageUrn);
    }

    [Fact]
    public void Models_profile_neutral_runtime_endpoint_intent()
    {
        var topology = new ReceiveEndpointTransportTopology(
            "orders",
            durable: true,
            temporary: false,
            prefetchCount: 16,
            [new MessageBinding { MessageType = typeof(OrderSubmitted), EntityName = "contracts-order-submitted" }]);

        Assert.Equal("orders", topology.Name);
        Assert.True(topology.Durable);
        Assert.False(topology.Temporary);
        Assert.Equal((ushort)16, topology.PrefetchCount);
        Assert.Equal("contracts-order-submitted", Assert.Single(topology.Bindings).EntityName);
    }

    [Fact]
    public void Moving_a_consumer_keeps_endpoint_topology_consistent()
    {
        var registry = new TopologyRegistry();
        registry.RegisterConsumer<OrderConsumer>("order-consumer", null, typeof(OrderSubmitted));

        registry.MoveConsumerToEndpoint(registry.Consumers.Single(), "orders");

        var snapshot = ((IBusTopology)registry).GetSnapshot();
        var endpoint = Assert.Single(snapshot.ReceiveEndpoints);
        Assert.Equal("orders", endpoint.Name);
        Assert.Equal(endpoint.Id, Assert.Single(snapshot.Consumers).EndpointId);
    }

    [Fact]
    public void Rejects_duplicate_or_unsupported_choreography_fragments()
    {
        var registry = new TopologyRegistry();
        var fragment = new ChoreographyBuilder("orders", "1", "orders")
            .Step<OrderSubmitted>("submit", step => step.Terminates())
            .Build();

        registry.RegisterChoreography(fragment);

        Assert.Throws<ArgumentException>(() => registry.RegisterChoreography(fragment));
        Assert.Throws<InvalidOperationException>(() =>
            new TopologyRegistry().RegisterChoreography(fragment with { SchemaVersion = 999 }));
    }

    private interface IOrderEvent;

    private sealed class OrderSubmitted : IOrderEvent;
    private sealed class OrderAccepted;

    private sealed class OrderConsumer;
}
