using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Orchestration;

namespace MyServiceBus.Tests;

public class SagaStateMachineBusRegistrationTests
{
    [Fact]
    public async Task Registers_events_and_dispatches_outgoing_work_through_the_consume_context()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(configurator =>
            configurator.AddSagaStateMachine<OrderStateMachine, OrderState>());
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        var repository = provider.GetRequiredService<InMemorySagaRepository<OrderState>>();
        var reserveInventory = new List<ReserveInventory>();
        var orderCompleted = new List<OrderCompleted>();
        harness.RegisterHandler<ReserveInventory>(context =>
        {
            reserveInventory.Add(context.Message);
            return Task.CompletedTask;
        });
        harness.RegisterHandler<OrderCompleted>(context =>
        {
            orderCompleted.Add(context.Message);
            return Task.CompletedTask;
        });
        await harness.Start();
        var orderId = Guid.NewGuid();

        await harness.Publish(new OrderSubmitted(orderId));

        Assert.True(repository.TryGet(orderId, out var awaitingPayment));
        Assert.Equal("AwaitingPayment", awaitingPayment!.CurrentState);
        Assert.Equal(orderId, Assert.Single(reserveInventory).OrderId);

        await harness.Publish(new PaymentReceived(orderId));

        Assert.Equal(0, repository.Count);
        Assert.Equal(orderId, Assert.Single(orderCompleted).OrderId);
        var sagaConsumers = harness.Topology.Consumers
            .Where(consumer => consumer.ConsumerType.Name.StartsWith("SagaStateMachineConsumer", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, sagaConsumers.Length);
        Assert.All(sagaConsumers, consumer => Assert.Equal("order-state-machine", consumer.QueueName));
        var sagaTopology = Assert.Single(harness.Topology.SagaStateMachines);
        Assert.Equal("order-state-machine", sagaTopology.Definition.StateMachineId);
        Assert.Equal("orders", sagaTopology.Definition.Owner);
        Assert.Equal("order-state-machine", sagaTopology.EndpointName);
        await harness.Stop();
    }

    public sealed class OrderStateMachine : SagaStateMachine<OrderState>
    {
        public OrderStateMachine()
            : base("order-state-machine", "1", "orders")
        {
            InstanceState(state => state.CurrentState, (state, value) => state.CurrentState = value);
            InstanceFactory(id => new OrderState { CorrelationId = id });
            CloneInstance(state => state.Copy());

            var awaitingPayment = State("AwaitingPayment");
            var orderSubmitted = Event<OrderSubmitted>("OrderSubmitted", correlation => correlation
                .CorrelateById("CorrelationId", "OrderId", message => message.OrderId)
                .CreatesIfMissing());
            var paymentReceived = Event<PaymentReceived>("PaymentReceived", correlation => correlation
                .CorrelateById("CorrelationId", "OrderId", message => message.OrderId));

            Initially(When(orderSubmitted)
                .Send(
                    MessageUrn.For(typeof(ReserveInventory)),
                    "queue:reserve-inventory",
                    context => new ReserveInventory(context.Message.OrderId))
                .TransitionTo(awaitingPayment));
            During(awaitingPayment, When(paymentReceived)
                .Publish(
                    MessageUrn.For(typeof(OrderCompleted)),
                    context => new OrderCompleted(context.Message.OrderId))
                .Finalize());
            DeleteWhenFinalized();
        }
    }

    public sealed class OrderState
    {
        public Guid CorrelationId { get; init; }
        public string? CurrentState { get; set; }

        public OrderState Copy() => new()
        {
            CorrelationId = CorrelationId,
            CurrentState = CurrentState
        };
    }

    public sealed record OrderSubmitted(Guid OrderId);
    public sealed record PaymentReceived(Guid OrderId);
    public sealed record ReserveInventory(Guid OrderId);
    public sealed record OrderCompleted(Guid OrderId);
}
