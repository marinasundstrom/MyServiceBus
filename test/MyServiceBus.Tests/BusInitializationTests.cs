using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.Tests;

public class BusInitializationTests
{
    public record LifecycleOrder(string Id);
    public class LifecycleConsumer : IConsumer<LifecycleOrder>
    {
        public static int Deliveries;
        public Task Consume(ConsumeContext<LifecycleOrder> context) { Interlocked.Increment(ref Deliveries); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Standalone_start_initializes_consumers_once_across_restart()
    {
        LifecycleConsumer.Deliveries = 0;
        var services = new ServiceCollection();
        services.AddServiceBus(cfg => { cfg.AddConsumer<LifecycleConsumer>(); cfg.UsingMediator(); });
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await Task.WhenAll(bus.StartAsync(CancellationToken.None), bus.StartAsync(CancellationToken.None));
        await bus.Publish(new LifecycleOrder("first"));
        await bus.StopAsync(CancellationToken.None);
        await bus.StartAsync(CancellationToken.None);
        await bus.Publish(new LifecycleOrder("second"));
        await bus.StopAsync(CancellationToken.None);
        Assert.Equal(2, LifecycleConsumer.Deliveries);
    }
}
