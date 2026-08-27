using NServiceBus;

namespace TestApp;

public sealed class NServiceBusSubmitOrderHandler : IHandleMessages<SubmitOrder>
{
    public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Received SubmitOrder {message.OrderId} from {message.Message}");
        await context.Publish(new OrderSubmitted(message.OrderId, "NServiceBus"));
    }
}

public sealed class NServiceBusOrderSubmittedHandler : IHandleMessages<OrderSubmitted>
{
    public Task Handle(OrderSubmitted message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Received OrderSubmitted {message.OrderId}");
        return Task.CompletedTask;
    }
}

public sealed class NServiceBusTestRequestHandler : IHandleMessages<TestRequest>
{
    public Task Handle(TestRequest message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Received TestRequest {message.Message}");
        return context.Reply(new TestResponse { Message = "NServiceBus" });
    }
}
