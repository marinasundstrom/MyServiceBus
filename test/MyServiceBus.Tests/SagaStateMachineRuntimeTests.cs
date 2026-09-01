using System.Text.Json;
using System.Text.Json.Nodes;
using MyServiceBus.Orchestration;

namespace MyServiceBus.Tests;

public class SagaStateMachineRuntimeTests
{
    private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Executes_the_canonical_cross_language_delivery_sequence()
    {
        var repository = new InMemorySagaRepository<OrderState>(state => state.Copy());
        var runtime = CreateRuntime(repository);

        var results = new[]
        {
            await runtime.Deliver(new OrderSubmitted(OrderId)),
            await runtime.Deliver(new PaymentReceived(OrderId)),
            await runtime.Deliver(new ProcessingCompleted(OrderId)),
            await runtime.Deliver(new PaymentReceived(OrderId))
        };

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "state-machine-fixtures", "basic-order-sequence.json");
        var expected = JsonNode.Parse(await File.ReadAllBytesAsync(fixturePath))!["deliveries"];
        var actual = JsonSerializer.SerializeToNode(results);

        Assert.True(JsonNode.DeepEquals(expected, actual));
        Assert.Equal(0, repository.Count);
        Assert.IsType<ReserveInventory>(results[0].Outgoing[0].Message);
        Assert.IsType<OrderCompleted>(results[2].Outgoing[0].Message);
    }

    [Fact]
    public async Task Rejects_an_event_not_accepted_in_the_current_state_without_mutating_the_instance()
    {
        var repository = new InMemorySagaRepository<OrderState>(state => state.Copy());
        var runtime = CreateRuntime(repository);
        await runtime.Deliver(new OrderSubmitted(OrderId));

        await Assert.ThrowsAsync<SagaEventNotAcceptedException>(async () =>
            await runtime.Deliver(new OrderSubmitted(OrderId)));

        Assert.True(repository.TryGet(OrderId, out var instance));
        Assert.Equal("AwaitingPayment", instance!.CurrentState);
        Assert.Equal(OrderId, instance.OrderId);
    }

    [Fact]
    public async Task Rolls_back_mutation_and_outgoing_work_when_an_activity_fails()
    {
        var repository = new InMemorySagaRepository<OrderState>(state => state.Copy());
        var runtime = CreateRuntime(repository, failCapture: true);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runtime.Deliver(new OrderSubmitted(OrderId)));

        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public async Task Faults_when_an_existing_only_event_has_no_instance()
    {
        var repository = new InMemorySagaRepository<OrderState>(state => state.Copy());
        var runtime = CreateRuntime(repository);

        await Assert.ThrowsAsync<SagaMissingInstanceException>(async () =>
            await runtime.Deliver(new ProcessingCompleted(OrderId)));
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public async Task Prefers_the_exact_state_behavior_before_during_any_and_then_ignores()
    {
        var repository = new InMemorySagaRepository<OrderState>(state => state.Copy());
        var definition = new SagaStateMachineDefinitionBuilder(
                "selection",
                "1",
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
            .State("Other")
            .State("Running")
            .Event<Start>("Start", @event => @event
                .CorrelateById("CorrelationId", "OrderId")
                .CreatesIfMissing())
            .Event<Ping>("Ping", @event => @event
                .CorrelateById("CorrelationId", "OrderId"))
            .Initially("Start", behavior => behavior.TransitionTo("Running"))
            .During("Running", "Ping", behavior => behavior.TransitionTo("Other"))
            .DuringAny("Ping", behavior => behavior.Ignore())
            .Build();
        var runtime = new SagaStateMachineRuntimeBuilder<OrderState>(
                definition,
                repository,
                id => new OrderState { CorrelationId = id },
                state => state.CurrentState,
                (state, currentState) => state.CurrentState = currentState)
            .Event<Start>("Start", message => message.OrderId)
            .Event<Ping>("Ping", message => message.OrderId)
            .Build();

        await runtime.Deliver(new Start(OrderId));
        var exact = await runtime.Deliver(new Ping(OrderId));
        var ignored = await runtime.Deliver(new Ping(OrderId));

        Assert.Equal("Other", exact.EndState);
        Assert.Equal(SagaDeliveryStatus.Ignored, ignored.Status);
        Assert.True(repository.TryGet(OrderId, out var instance));
        Assert.Equal("Other", instance!.CurrentState);
    }

    [Fact]
    public async Task Does_not_apply_during_any_to_a_retained_final_instance()
    {
        var repository = new InMemorySagaRepository<OrderState>(state => state.Copy());
        var definition = new SagaStateMachineDefinitionBuilder(
                "final-selection",
                "1",
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
            .State("Running")
            .Event<Start>("Start", @event => @event
                .CorrelateById("CorrelationId", "OrderId")
                .CreatesIfMissing())
            .Event<Finish>("Finish", @event => @event
                .CorrelateById("CorrelationId", "OrderId"))
            .Event<Ping>("Ping", @event => @event
                .CorrelateById("CorrelationId", "OrderId"))
            .Initially("Start", behavior => behavior.TransitionTo("Running"))
            .During("Running", "Finish", behavior => behavior.Finalize())
            .DuringAny("Ping", behavior => behavior.Ignore())
            .Build();
        var runtime = new SagaStateMachineRuntimeBuilder<OrderState>(
                definition,
                repository,
                id => new OrderState { CorrelationId = id },
                state => state.CurrentState,
                (state, currentState) => state.CurrentState = currentState)
            .Event<Start>("Start", message => message.OrderId)
            .Event<Finish>("Finish", message => message.OrderId)
            .Event<Ping>("Ping", message => message.OrderId)
            .Build();

        await runtime.Deliver(new Start(OrderId));
        var completed = await runtime.Deliver(new Finish(OrderId));

        Assert.True(completed.Completed);
        Assert.True(completed.InstancePresent);
        await Assert.ThrowsAsync<SagaEventNotAcceptedException>(async () =>
            await runtime.Deliver(new Ping(OrderId)));
    }

    private static SagaStateMachineRuntime<OrderState> CreateRuntime(
        InMemorySagaRepository<OrderState> repository,
        bool failCapture = false)
    {
        var definition = CreateDefinition();
        return new SagaStateMachineRuntimeBuilder<OrderState>(
                definition,
                repository,
                id => new OrderState { CorrelationId = id },
                state => state.CurrentState,
                (state, currentState) => state.CurrentState = currentState)
            .Event<OrderSubmitted>("OrderSubmitted", message => message.OrderId)
            .Event<PaymentReceived>("PaymentReceived", message => message.OrderId)
            .Event<ProcessingCompleted>("ProcessingCompleted", message => message.OrderId)
            .Mutate<OrderSubmitted>("Initial", "OrderSubmitted", 0, (context, _) =>
            {
                context.Saga.OrderId = context.Message.OrderId;
                if (failCapture)
                    throw new InvalidOperationException("capture failed");
                return ValueTask.CompletedTask;
            })
            .Message<OrderSubmitted, ReserveInventory>("Initial", "OrderSubmitted", 1, (context, _) =>
                ValueTask.FromResult(new ReserveInventory(context.Saga.OrderId)))
            .Mutate<PaymentReceived>("AwaitingPayment", "PaymentReceived", 0, (context, _) =>
            {
                context.Saga.PaymentReceived = true;
                return ValueTask.CompletedTask;
            })
            .Message<ProcessingCompleted, OrderCompleted>("Processing", "ProcessingCompleted", 0, (context, _) =>
                ValueTask.FromResult(new OrderCompleted(context.Message.OrderId)))
            .Build();
    }

    private static SagaStateMachineDefinition CreateDefinition() => new SagaStateMachineDefinitionBuilder(
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

    private sealed class OrderState
    {
        public Guid CorrelationId { get; init; }
        public Guid OrderId { get; set; }
        public string? CurrentState { get; set; }
        public bool PaymentReceived { get; set; }

        public OrderState Copy() => new()
        {
            CorrelationId = CorrelationId,
            OrderId = OrderId,
            CurrentState = CurrentState,
            PaymentReceived = PaymentReceived
        };
    }

    private sealed record OrderSubmitted(Guid OrderId);
    private sealed record PaymentReceived(Guid OrderId);
    private sealed record ProcessingCompleted(Guid OrderId);
    private sealed record ReserveInventory(Guid OrderId);
    private sealed record OrderCompleted(Guid OrderId);
    private sealed record Start(Guid OrderId);
    private sealed record Ping(Guid OrderId);
    private sealed record Finish(Guid OrderId);
}
