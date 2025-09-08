using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;

public class MultipleConsumersTests
{
    record SubmitOrder(Guid OrderId);

    class FirstConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    class SecondConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    [Fact]
    [Throws(typeof(InvalidOperationException))]
    public async Task Should_invoke_all_consumers()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(x =>
        {
            x.AddConsumer<FirstConsumer>();
            x.AddConsumer<SecondConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();

        await harness.Start();
        await harness.Publish(new SubmitOrder(Guid.NewGuid()));

        Assert.True(harness.Consumed.OfType<SubmitOrder>().Count() == 2);

        await harness.Stop();
    }
}
