using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Orchestration;
using TestApp;

namespace MyServiceBus.Tests;

public class OrderOrchestrationSampleTests
{
    [Fact]
    public async Task Completes_the_cross_service_order_scenario()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(configurator =>
            configurator.AddSagaStateMachine<OrderOrchestrationStateMachine, OrderOrchestrationState>());
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        var repository = provider.GetRequiredService<InMemorySagaRepository<OrderOrchestrationState>>();
        var inventoryRequests = new List<OrchestrationInventoryRequested>();
        var paymentRequests = new List<OrchestrationPaymentRequested>();
        var completed = new List<OrderOrchestrationCompleted>();
        harness.RegisterHandler<OrchestrationInventoryRequested>(context =>
        {
            inventoryRequests.Add(context.Message);
            return Task.CompletedTask;
        });
        harness.RegisterHandler<OrchestrationPaymentRequested>(context =>
        {
            paymentRequests.Add(context.Message);
            return Task.CompletedTask;
        });
        harness.RegisterHandler<OrderOrchestrationCompleted>(context =>
        {
            completed.Add(context.Message);
            return Task.CompletedTask;
        });
        await harness.Start();
        var orderId = Guid.NewGuid();

        await harness.Publish(new OrderOrchestrationStarted(orderId));
        Assert.Equal(orderId, Assert.Single(inventoryRequests).OrderId);
        Assert.True(repository.TryGet(orderId, out var awaitingInventory));
        Assert.Equal("AwaitingInventory", awaitingInventory!.CurrentState);

        await harness.Publish(new OrchestrationInventoryReserved(orderId));
        Assert.Equal(orderId, Assert.Single(paymentRequests).OrderId);
        Assert.True(repository.TryGet(orderId, out var awaitingPayment));
        Assert.Equal("AwaitingPayment", awaitingPayment!.CurrentState);

        await harness.Publish(new OrchestrationPaymentCaptured(orderId));

        Assert.Equal(orderId, Assert.Single(completed).OrderId);
        Assert.Equal(0, repository.Count);
        await harness.Stop();
    }
}
