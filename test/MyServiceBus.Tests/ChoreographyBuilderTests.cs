using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Choreography;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public class ChoreographyBuilderTests
{
    [Fact]
    public void Builds_the_canonical_cross_language_fragment()
    {
        var fragment = new ChoreographyBuilder("order-fulfillment", "1", "orders")
            .Step("reserve-inventory", "urn:message:Contracts:OrderSubmitted", step => step
                .OwnedBy("submit-order-consumer")
                .Publishes("urn:message:Contracts:OrderAccepted", output => output.Optional().AtMost(1))
                .Sends("urn:message:Contracts:ReserveInventory", "queue:reserve-inventory", output => output
                    .Expected()
                    .Exactly(1)
                    .Within(TimeSpan.FromSeconds(5))))
            .Step("complete-order", "urn:message:Contracts:OrderCompleted", step => step
                .OwnedBy("complete-order-consumer")
                .Terminates())
            .Build();

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "choreography-fixtures", "basic-choreography.json");
        var fixture = File.ReadAllBytes(fixturePath);
        var expected = JsonNode.Parse(fixture);
        var actual = JsonSerializer.SerializeToNode(fragment);

        Assert.True(JsonNode.DeepEquals(expected, actual));

        var deserialized = JsonSerializer.Deserialize<ChoreographyFragment>(fixture);
        Assert.NotNull(deserialized);
        Assert.Equal("order-fulfillment", deserialized.ChoreographyId);
        Assert.Equal(ChoreographyOperationKind.Terminal, deserialized.Steps[0].Outputs[0].Kind);
    }

    [Fact]
    public void Type_based_builder_uses_message_urns_and_component_identity()
    {
        var fragment = new ChoreographyBuilder("orders", "1", "orders")
            .Step<OrderSubmitted>("submit", step => step
                .OwnedBy<OrderConsumer>()
                .Publishes<OrderAccepted>())
            .Build();

        var step = Assert.Single(fragment.Steps);
        Assert.Equal(MessageUrn.For(typeof(OrderSubmitted)), step.TriggerMessageUrn);
        Assert.Equal(typeof(OrderConsumer).FullName, step.OwnerComponent);
        Assert.Equal(MessageUrn.For(typeof(OrderAccepted)), Assert.Single(step.Outputs).MessageUrn);
    }

    [Fact]
    public void Rejects_invalid_or_ambiguous_declarations()
    {
        var duplicate = new ChoreographyBuilder("orders", "1", "orders")
            .Step<OrderSubmitted>("submit", step => step.Terminates());

        Assert.Throws<ArgumentException>(() => duplicate.Step<OrderSubmitted>("submit", step => step.Terminates()));
        Assert.Throws<InvalidOperationException>(() => new ChoreographyBuilder("orders", "1", "orders").Build());
        Assert.Throws<InvalidOperationException>(() => new ChoreographyBuilder("orders", "1", "orders")
            .Step<OrderSubmitted>("submit", step => step.Publishes<OrderAccepted>(output => output.AtLeast(2).AtMost(1))));
    }

    [Fact]
    public void Registers_a_fragment_with_the_bus_topology()
    {
        var fragment = new ChoreographyBuilder("orders", "1", "orders")
            .Step<OrderSubmitted>("submit", step => step.Publishes<OrderAccepted>())
            .Build();
        var services = new ServiceCollection();

        services.AddServiceBus(configurator =>
        {
            configurator.AddChoreography(fragment);
            configurator.UsingMediator();
        });

        using var provider = services.BuildServiceProvider();
        Assert.Same(fragment, Assert.Single(provider.GetRequiredService<IBusTopology>().Choreographies));
    }

    private sealed class OrderSubmitted;
    private sealed class OrderAccepted;
    private sealed class OrderConsumer;
}
