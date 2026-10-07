using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.Tests;

public class IntegrationPolicyTests
{
    public record LocalOrder(int Quantity);
    public class OrderConsumer : IConsumer<LocalOrder>
    {
        public static int Attempts;
        public Task Consume(ConsumeContext<LocalOrder> context)
        {
            Assert.NotNull(context.MessageId);
            if (Interlocked.Increment(ref Attempts) == 1) throw new InvalidOperationException("retry");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Hostless_start_runtime_publish_and_definition_policy_use_one_contract()
    {
        OrderConsumer.Attempts = 0;
        var services = new ServiceCollection();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            var definition = new ConsumerDefinition<OrderConsumer> { EndpointName = "orders", ConcurrentMessageLimit = 2 };
            definition.ConfigureMessage<LocalOrder>(pipe => pipe.UseRetry(1));
            cfg.AddConsumer(definition);
        });
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await Task.WhenAll(bus.StartAsync(CancellationToken.None), bus.StartAsync(CancellationToken.None));
        object order = new LocalOrder(3);
        await ((IPublishEndpoint)bus).Publish(order, typeof(LocalOrder));
        Assert.Equal(2, OrderConsumer.Attempts);
        await bus.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Runtime_consumer_registration_uses_normal_pipeline()
    {
        OrderConsumer.Attempts = 1;
        var services = new ServiceCollection();
        services.AddServiceBus(cfg => { cfg.AddConsumer(typeof(OrderConsumer)); cfg.UsingMediator(); });
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.StartAsync(CancellationToken.None);
        await bus.Publish(new LocalOrder(1));
        await bus.StopAsync(CancellationToken.None);
        Assert.Equal(2, OrderConsumer.Attempts);
    }
}
