using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class AddConsumersTests
{
    record Ping(Guid Id);

    class PingConsumer : IConsumer<Ping>
    {
        public Task Consume(ConsumeContext<Ping> context) => Task.CompletedTask;
    }

    [Fact]
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException), typeof(TrueException))]
    public async Task Should_register_all_consumers_from_assembly()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(x =>
        {
            x.AddConsumers(typeof(AddConsumersTests).Assembly);
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();

        await harness.Start();

        await harness.PublishAsync(new Ping(Guid.NewGuid()));

        Assert.True(harness.WasConsumed<Ping>());

        await harness.Stop();
    }
}
