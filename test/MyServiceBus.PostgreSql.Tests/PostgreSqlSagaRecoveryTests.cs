using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Orchestration;
using MyServiceBus.Persistence;
using MyServiceBus.Persistence.PostgreSql;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MyServiceBus.PostgreSql.Tests;

public sealed class PostgreSqlSagaRecoveryTests : IAsyncLifetime
{
    private const string ServiceName = "saga-recovery-service";
    private const string SagaType = "tests.durable-order-saga";
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
    private NpgsqlDataSource dataSource = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await PostgreSqlSchema.EnsureCreatedAsync(dataSource);
    }

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Runtime_recovers_after_failure_and_restart_serializes_concurrent_delivery_and_deletes_final_state()
    {
        var services = new ServiceCollection();
        services.AddServiceBus(configurator =>
        {
            configurator.UseBusOutbox();
            configurator.UsingMediator();
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.StartAsync(CancellationToken.None);

        try
        {
            var failedOrderId = Guid.NewGuid();
            await using (var failedScope = provider.CreateAsyncScope())
            {
                var failedRuntime = CreateRuntime(failedScope.ServiceProvider);
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await failedRuntime.Deliver(
                        new OrderSubmitted(failedOrderId),
                        async (operation, cancellationToken) =>
                        {
                            await Dispatch(failedScope.ServiceProvider, operation, cancellationToken);
                            throw new InvalidOperationException("fail after staging outgoing work");
                        }));
            }

            Assert.Null(await Load(failedOrderId));
            Assert.Equal(0, await CountOutboxMessages());

            var orderId = Guid.NewGuid();
            await using (var firstProcess = provider.CreateAsyncScope())
            {
                var runtime = CreateRuntime(firstProcess.ServiceProvider);
                var submitted = await runtime.Deliver(
                    new OrderSubmitted(orderId),
                    (operation, cancellationToken) =>
                        Dispatch(firstProcess.ServiceProvider, operation, cancellationToken));

                Assert.Equal("AwaitingPayment", submitted.EndState);
                Assert.True(submitted.Created);
            }

            await using (var replicaA = provider.CreateAsyncScope())
            await using (var replicaB = provider.CreateAsyncScope())
            {
                var deliveryA = CreateRuntime(replicaA.ServiceProvider)
                    .Deliver(new WorkObserved(orderId)).AsTask();
                var deliveryB = CreateRuntime(replicaB.ServiceProvider)
                    .Deliver(new WorkObserved(orderId)).AsTask();
                await Task.WhenAll(deliveryA, deliveryB);
            }

            var afterConcurrentDelivery = await Load(orderId);
            Assert.NotNull(afterConcurrentDelivery);
            Assert.Equal("AwaitingPayment", afterConcurrentDelivery.CurrentState);
            Assert.Equal(2, afterConcurrentDelivery.ObservedWork);

            await using (var secondProcess = provider.CreateAsyncScope())
            {
                var payment = await CreateRuntime(secondProcess.ServiceProvider)
                    .Deliver(new PaymentReceived(orderId));
                Assert.Equal("Processing", payment.EndState);
            }

            await using (var finalProcess = provider.CreateAsyncScope())
            {
                var completed = await CreateRuntime(finalProcess.ServiceProvider).Deliver(
                    new ProcessingCompleted(orderId),
                    (operation, cancellationToken) =>
                        Dispatch(finalProcess.ServiceProvider, operation, cancellationToken));
                Assert.True(completed.Completed);
                Assert.False(completed.InstancePresent);
            }

            Assert.Null(await Load(orderId));
            Assert.Equal(2, await CountOutboxMessages());
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }

    private SagaStateMachineRuntime<DurableOrderState> CreateRuntime(IServiceProvider services)
    {
        var repository = new PostgreSqlSagaRepository<DurableOrderState>(
            dataSource,
            services.GetRequiredService<OutboxSession>(),
            ServiceName,
            SagaType);
        return new SagaStateMachineRuntimeBuilder<DurableOrderState>(
                CreateDefinition(),
                repository,
                correlationId => new DurableOrderState { CorrelationId = correlationId },
                state => state.CurrentState,
                (state, currentState) => state.CurrentState = currentState)
            .Event<OrderSubmitted>("OrderSubmitted", message => message.OrderId)
            .Event<WorkObserved>("WorkObserved", message => message.OrderId)
            .Event<PaymentReceived>("PaymentReceived", message => message.OrderId)
            .Event<ProcessingCompleted>("ProcessingCompleted", message => message.OrderId)
            .Mutate<OrderSubmitted>("Initial", "OrderSubmitted", 0, (context, _) =>
            {
                context.Saga.OrderId = context.Message.OrderId;
                return ValueTask.CompletedTask;
            })
            .Message<OrderSubmitted, ReserveInventory>("Initial", "OrderSubmitted", 1, (context, _) =>
                ValueTask.FromResult(new ReserveInventory(context.Message.OrderId)))
            .Mutate<WorkObserved>("AwaitingPayment", "WorkObserved", 0, (context, _) =>
            {
                context.Saga.ObservedWork++;
                return ValueTask.CompletedTask;
            })
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
            "durable-order-state-machine",
            "1",
            ServiceName,
            "urn:message:Tests:DurableOrderState",
            "CurrentState")
        .DeleteWhenFinalized()
        .State("AwaitingPayment")
        .State("Processing")
        .Event<OrderSubmitted>("OrderSubmitted", @event => @event
            .CorrelateById("CorrelationId", "OrderId")
            .CreatesIfMissing())
        .Event<WorkObserved>("WorkObserved", @event => @event
            .CorrelateById("CorrelationId", "OrderId"))
        .Event<PaymentReceived>("PaymentReceived", @event => @event
            .CorrelateById("CorrelationId", "OrderId"))
        .Event<ProcessingCompleted>("ProcessingCompleted", @event => @event
            .CorrelateById("CorrelationId", "OrderId"))
        .Initially("OrderSubmitted", behavior => behavior
            .Mutate("Initial.OrderSubmitted.0")
            .Send("urn:message:Tests:ReserveInventory", "loopback://reserve-inventory")
            .TransitionTo("AwaitingPayment"))
        .During("AwaitingPayment", "WorkObserved", behavior => behavior
            .Mutate("AwaitingPayment.WorkObserved.0"))
        .During("AwaitingPayment", "PaymentReceived", behavior => behavior
            .Mutate("AwaitingPayment.PaymentReceived.0")
            .TransitionTo("Processing"))
        .During("Processing", "ProcessingCompleted", behavior => behavior
            .Publish("urn:message:Tests:OrderCompleted")
            .Finalize())
        .Build();

    private static async ValueTask Dispatch(
        IServiceProvider services,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken)
    {
        switch (operation.Message)
        {
            case ReserveInventory reserveInventory:
                var endpoint = await services.GetRequiredService<ISendEndpointProvider>()
                    .GetSendEndpoint(new Uri(operation.Destination!));
                await endpoint.Send(reserveInventory, cancellationToken: cancellationToken);
                break;
            case OrderCompleted orderCompleted:
                await services.GetRequiredService<IPublishEndpoint>()
                    .Publish(orderCompleted, cancellationToken: cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unexpected saga output '{operation.Message.GetType()}'.");
        }
    }

    private async Task<DurableOrderState?> Load(Guid correlationId)
    {
        var repository = new PostgreSqlSagaRepository<DurableOrderState>(
            dataSource, new OutboxSession(), ServiceName, SagaType);
        return await repository.Execute(
            correlationId,
            static (instance, _) => ValueTask.FromResult(
                SagaRepositoryTransaction<DurableOrderState, DurableOrderState?>.NoChange(instance)));
    }

    private async Task<long> CountOutboxMessages()
    {
        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM myservicebus.outbox_message WHERE service_name = @service_name;");
        command.Parameters.AddWithValue("service_name", ServiceName);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public sealed class DurableOrderState
    {
        public Guid CorrelationId { get; init; }
        public Guid OrderId { get; set; }
        public string? CurrentState { get; set; }
        public bool PaymentReceived { get; set; }
        public int ObservedWork { get; set; }
    }

    private sealed record OrderSubmitted(Guid OrderId);
    private sealed record WorkObserved(Guid OrderId);
    private sealed record PaymentReceived(Guid OrderId);
    private sealed record ProcessingCompleted(Guid OrderId);
    private sealed record ReserveInventory(Guid OrderId);
    private sealed record OrderCompleted(Guid OrderId);
}
