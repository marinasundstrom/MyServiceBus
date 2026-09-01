using MyServiceBus;
using MyServiceBus.Orchestration;

namespace TestApp;

public sealed class OrderOrchestrationStateMachine : SagaStateMachine<OrderOrchestrationState>
{
    public OrderOrchestrationStateMachine(bool requireDurableRepository = false)
        : base("sample-order-orchestration", "1", "TestApp.CSharp")
    {
        InstanceState(state => state.CurrentState, (state, value) => state.CurrentState = value);
        InstanceFactory(id => new OrderOrchestrationState { CorrelationId = id });
        CloneInstance(state => state.Copy());
        if (requireDurableRepository)
        {
            RepositoryRequirements(new SagaRepositoryRequirements(
                SagaCorrelationKind.Identity,
                SagaConcurrencyKind.Pessimistic,
                SagaDurabilityKind.Durable,
                SagaOutboxKind.Transactional));
        }

        var awaitingInventory = State("AwaitingInventory");
        var awaitingPayment = State("AwaitingPayment");
        var started = Event<OrderOrchestrationStarted>("OrderOrchestrationStarted", correlation => correlation
            .CorrelateById("CorrelationId", "OrderId", message => message.OrderId)
            .CreatesIfMissing());
        var inventoryReserved = Event<OrchestrationInventoryReserved>("OrchestrationInventoryReserved", correlation => correlation
            .CorrelateById("CorrelationId", "OrderId", message => message.OrderId));
        var paymentCaptured = Event<OrchestrationPaymentCaptured>("OrchestrationPaymentCaptured", correlation => correlation
            .CorrelateById("CorrelationId", "OrderId", message => message.OrderId));

        Initially(When(started)
            .Send(
                MessageUrn.For(typeof(OrchestrationInventoryRequested)),
                "queue:OrchestrationInventoryRequested",
                context => new OrchestrationInventoryRequested(context.Message.OrderId))
            .TransitionTo(awaitingInventory));
        During(awaitingInventory, When(inventoryReserved)
            .Send(
                MessageUrn.For(typeof(OrchestrationPaymentRequested)),
                "queue:OrchestrationPaymentRequested",
                context => new OrchestrationPaymentRequested(context.Message.OrderId))
            .TransitionTo(awaitingPayment));
        During(awaitingPayment, When(paymentCaptured)
            .Publish(
                MessageUrn.For(typeof(OrderOrchestrationCompleted)),
                context => new OrderOrchestrationCompleted(context.Message.OrderId))
            .Finalize());
        DeleteWhenFinalized();
    }
}

public sealed class OrderOrchestrationState
{
    public Guid CorrelationId { get; init; }
    public string? CurrentState { get; set; }

    public OrderOrchestrationState Copy() => new()
    {
        CorrelationId = CorrelationId,
        CurrentState = CurrentState
    };
}

internal sealed class OrchestrationPaymentRequestedConsumer : IConsumer<OrchestrationPaymentRequested>
{
    public Task Consume(ConsumeContext<OrchestrationPaymentRequested> context)
        => context.Publish(new OrchestrationPaymentCaptured(context.Message.OrderId));
}

internal sealed class OrderOrchestrationCompletedConsumer : IConsumer<OrderOrchestrationCompleted>
{
    public Task Consume(ConsumeContext<OrderOrchestrationCompleted> context)
        => Task.CompletedTask;
}
