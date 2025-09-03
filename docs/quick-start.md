# Quick Start

This guide shows the minimal steps to configure MyServiceBus and publish a message.

## C#

```csharp
public record SubmitOrder(Guid OrderId);

class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingRabbitMq((context, cfg) => cfg.ConfigureEndpoints(context));
});

var app = builder.Build();
await app.StartAsync();

await app.Services.GetRequiredService<IPublishEndpoint>()
    .Publish(new SubmitOrder(Guid.NewGuid()));
```

## Java

```java
record SubmitOrder(UUID orderId) { }

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> ctx) {
        return CompletableFuture.completedFuture(null);
    }
}

ServiceCollection services = new ServiceCollection();
RabbitMqBusFactory.configure(services, x -> {
    x.addConsumer(SubmitOrderConsumer.class);
}, (context, cfg) -> cfg.host("rabbitmq://localhost"));

ServiceBus bus = services.build().getService(ServiceBus.class);
bus.start();

bus.publish(new SubmitOrder(UUID.randomUUID()), CancellationToken.none);
```
