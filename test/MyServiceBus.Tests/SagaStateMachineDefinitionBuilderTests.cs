using System.Text.Json;
using System.Text.Json.Nodes;
using MyServiceBus.Orchestration;

namespace MyServiceBus.Tests;

public class SagaStateMachineDefinitionBuilderTests
{
    [Fact]
    public void Builds_the_canonical_cross_language_definition()
    {
        var definition = new SagaStateMachineDefinitionBuilder(
                "order-state-machine",
                "1",
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
            .DeleteWhenFinalized()
            .State("Processing")
            .State("AwaitingPayment")
            .Event("ProcessingCompleted", "urn:message:Contracts:ProcessingCompleted", @event => @event
                .CorrelateById("CorrelationId", "OrderId")
                .FaultIfMissing())
            .Event("OrderSubmitted", "urn:message:Contracts:OrderSubmitted", @event => @event
                .CorrelateById("CorrelationId", "OrderId")
                .CreatesIfMissing()
                .FaultIfMissing())
            .Event("PaymentReceived", "urn:message:Contracts:PaymentReceived", @event => @event
                .CorrelateById("CorrelationId", "OrderId")
                .DiscardIfMissing())
            .During("Processing", "ProcessingCompleted", behavior => behavior
                .Publish("urn:message:Contracts:OrderCompleted")
                .Finalize())
            .Initially("OrderSubmitted", behavior => behavior
                .Mutate("capture-order")
                .Send("urn:message:Contracts:ReserveInventory", "queue:reserve-inventory")
                .TransitionTo("AwaitingPayment"))
            .During("AwaitingPayment", "PaymentReceived", behavior => behavior
                .Mutate("record-payment")
                .TransitionTo("Processing"))
            .Build();

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "state-machine-fixtures", "basic-order-state-machine.json");
        var fixture = File.ReadAllBytes(fixturePath);
        var expected = JsonNode.Parse(fixture);
        var actual = JsonSerializer.SerializeToNode(definition);

        Assert.True(JsonNode.DeepEquals(expected, actual));

        var deserialized = JsonSerializer.Deserialize<SagaStateMachineDefinition>(fixture);
        Assert.NotNull(deserialized);
        deserialized.Validate();
        Assert.Equal(SagaCompletionPolicy.DeleteWhenFinalized, deserialized.CompletionPolicy);
        Assert.Equal(SagaActivityKind.Finalize, deserialized.Behaviors[2].Activities[1].Kind);
    }

    [Fact]
    public void Rejects_creation_without_initial_behavior()
    {
        var builder = MinimalBuilder()
            .Event<OrderSubmitted>("OrderSubmitted", @event => @event
                .CorrelateById("CorrelationId", "OrderId")
                .CreatesIfMissing())
            .During("Running", "OrderSubmitted", behavior => behavior.Finalize());

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("must declare an Initial behavior", exception.Message);
    }

    [Fact]
    public void Rejects_activity_after_transition()
    {
        var builder = new SagaBehaviorDefinitionBuilderAccessor();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddAfterTransition());
        Assert.Contains("No activity can follow", exception.Message);
    }

    private static SagaStateMachineDefinitionBuilder MinimalBuilder() => new SagaStateMachineDefinitionBuilder(
        "orders",
        "1",
        "orders",
        "urn:message:Contracts:OrderState",
        "CurrentState").State("Running");

    private sealed class SagaBehaviorDefinitionBuilderAccessor
    {
        public void AddAfterTransition()
        {
            MinimalBuilder()
                .Event<OrderSubmitted>("OrderSubmitted", @event => @event
                    .CorrelateById("CorrelationId", "OrderId")
                    .CreatesIfMissing())
                .Initially("OrderSubmitted", behavior => behavior
                    .TransitionTo("Running")
                    .Mutate("too-late"));
        }
    }

    private sealed class OrderSubmitted;
}
