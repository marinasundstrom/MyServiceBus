using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MyServiceBus;

public class MultipleConsumersFaultTests
{
    record SubmitOrder(Guid OrderId);

    class FirstConsumer : IConsumer<SubmitOrder>
    {
        public static int Calls;
        public Task Consume(ConsumeContext<SubmitOrder> context)
        {
            Calls++;
            throw new InvalidOperationException("boom");
        }
    }

    class SecondConsumer : IConsumer<SubmitOrder>
    {
        public static int Calls;
        public Task Consume(ConsumeContext<SubmitOrder> context)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    [Throws(typeof(InvalidOperationException))]
    public async Task Should_stop_after_first_consumer_faults()
    {
        FirstConsumer.Calls = 0;
        SecondConsumer.Calls = 0;

        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(x =>
        {
            x.AddConsumer<FirstConsumer>();
            x.AddConsumer<SecondConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();

        await harness.Start();
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.PublishAsync(new SubmitOrder(Guid.NewGuid())));

        Assert.Equal(1, FirstConsumer.Calls);
        Assert.Equal(0, SecondConsumer.Calls);

        await harness.Stop();
    }
}
