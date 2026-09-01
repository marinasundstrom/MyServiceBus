using System.Text.Json;
using System.Text.Json.Nodes;
using MyServiceBus.Orchestration;

namespace MyServiceBus.Tests;

public class SagaStateMachineDslTests
{
    private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Lowers_and_executes_the_canonical_machine()
    {
        var machine = new OrderStateMachine();
        var definitionFixture = JsonNode.Parse(await File.ReadAllBytesAsync(Path.Combine(
            AppContext.BaseDirectory,
            "state-machine-fixtures",
            "basic-order-state-machine.json")));

        Assert.True(JsonNode.DeepEquals(definitionFixture, JsonSerializer.SerializeToNode(machine.Definition)));

        var repository = machine.CreateRepository();
        var runtime = machine.CreateRuntime(repository);
        var results = new[]
        {
            await runtime.Deliver(new OrderSubmitted(OrderId)),
            await runtime.Deliver(new PaymentReceived(OrderId)),
            await runtime.Deliver(new ProcessingCompleted(OrderId)),
            await runtime.Deliver(new PaymentReceived(OrderId))
        };
        var sequenceFixture = JsonNode.Parse(await File.ReadAllBytesAsync(Path.Combine(
            AppContext.BaseDirectory,
            "state-machine-fixtures",
            "basic-order-sequence.json")))!["deliveries"];

        Assert.True(JsonNode.DeepEquals(sequenceFixture, JsonSerializer.SerializeToNode(results)));
    }

    [Fact]
    public void Rejects_a_repository_that_cannot_meet_declared_requirements()
    {
        var machine = new OrderStateMachine(requiresDurableRepository: true);
        var repository = machine.CreateRepository();

        var exception = Assert.Throws<SagaRepositoryCapabilityException>(() => machine.CreateRuntime(repository));

        Assert.Equal("in-memory", exception.Provider);
        Assert.Contains("durable storage", exception.UnsupportedCapabilities);
        Assert.Contains("transactional outbox", exception.UnsupportedCapabilities);
    }

    private sealed class OrderStateMachine : SagaStateMachine<OrderState>
    {
        public OrderStateMachine(bool requiresDurableRepository = false)
            : base("order-state-machine", "1", "orders", "urn:message:Contracts:OrderState")
        {
            InstanceState(state => state.CurrentState, (state, value) => state.CurrentState = value);
            InstanceFactory(id => new OrderState { CorrelationId = id });
            CloneInstance(state => state.Copy());
            if (requiresDurableRepository)
            {
                RepositoryRequirements(new SagaRepositoryRequirements(
                    SagaCorrelationKind.Identity,
                    SagaConcurrencyKind.Optimistic,
                    SagaDurabilityKind.Durable,
                    SagaOutboxKind.Transactional));
            }

            var awaitingPayment = State("AwaitingPayment");
            var processing = State("Processing");
            var orderSubmitted = Event<OrderSubmitted>(
                "OrderSubmitted",
                "urn:message:Contracts:OrderSubmitted",
                correlation => correlation
                    .CorrelateById("CorrelationId", "OrderId", message => message.OrderId)
                    .CreatesIfMissing()
                    .FaultIfMissing());
            var paymentReceived = Event<PaymentReceived>(
                "PaymentReceived",
                "urn:message:Contracts:PaymentReceived",
                correlation => correlation
                    .CorrelateById("CorrelationId", "OrderId", message => message.OrderId)
                    .DiscardIfMissing());
            var processingCompleted = Event<ProcessingCompleted>(
                "ProcessingCompleted",
                "urn:message:Contracts:ProcessingCompleted",
                correlation => correlation
                    .CorrelateById("CorrelationId", "OrderId", message => message.OrderId));

            Initially(
                When(orderSubmitted)
                    .Then(context => context.Saga.OrderId = context.Message.OrderId)
                    .Send(
                        "urn:message:Contracts:ReserveInventory",
                        "queue:reserve-inventory",
                        context => new ReserveInventory(context.Saga.OrderId))
                    .TransitionTo(awaitingPayment));
            During(
                awaitingPayment,
                When(paymentReceived)
                    .Then(context => context.Saga.PaymentReceived = true)
                    .TransitionTo(processing));
            During(
                processing,
                When(processingCompleted)
                    .Publish(
                        "urn:message:Contracts:OrderCompleted",
                        context => new OrderCompleted(context.Message.OrderId))
                    .Finalize());
            DeleteWhenFinalized();
        }

        public InMemorySagaRepository<OrderState> CreateRepository() => CreateInMemoryRepository();
    }

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
}
