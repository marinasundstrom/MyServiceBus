using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class DuplicateConsumerRegistrationTests
{
    record SubmitOrder(Guid OrderId);

    class SubmitOrderConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    [Fact]
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException), typeof(TrueException))]
    public async Task Should_ignore_duplicate_consumer_registration()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(x =>
        {
            x.AddConsumer<SubmitOrderConsumer>();
            x.AddConsumer<SubmitOrderConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();

        await harness.Start();
        await harness.Publish(new SubmitOrder(Guid.NewGuid()));

        Assert.True(harness.Consumed.OfType<SubmitOrder>().Count() == 1);

        await harness.Stop();
    }
}
