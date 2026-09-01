using MyServiceBus;

namespace TestApp;

internal sealed class FulfillmentRequestedConsumer : IConsumer<FulfillmentRequested>
{
    public Task Consume(ConsumeContext<FulfillmentRequested> context)
        => context.Publish(new InventoryReservationRequested(context.Message.OrderId));
}

internal sealed class InventoryReservedConsumer : IConsumer<InventoryReserved>
{
    public Task Consume(ConsumeContext<InventoryReserved> context)
        => context.Publish(new FulfillmentCompleted(context.Message.OrderId));
}

internal sealed class FulfillmentCompletedConsumer : IConsumer<FulfillmentCompleted>
{
    public Task Consume(ConsumeContext<FulfillmentCompleted> context)
        => Task.CompletedTask;
}
