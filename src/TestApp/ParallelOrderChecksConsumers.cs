using MyServiceBus;

namespace TestApp;

internal sealed class ParallelOrderChecksRequestedConsumer : IConsumer<ParallelOrderChecksRequested>
{
    public Task Consume(ConsumeContext<ParallelOrderChecksRequested> context)
        => Task.WhenAll(
            context.Publish(new PaymentCheckRequested(context.Message.OrderId)),
            context.Publish(new InventoryCheckRequested(context.Message.OrderId)));
}

internal sealed class PaymentCheckRequestedConsumer : IConsumer<PaymentCheckRequested>
{
    public Task Consume(ConsumeContext<PaymentCheckRequested> context) => Task.CompletedTask;
}

internal sealed class InventoryCheckRequestedConsumer : IConsumer<InventoryCheckRequested>
{
    public Task Consume(ConsumeContext<InventoryCheckRequested> context) => Task.CompletedTask;
}
