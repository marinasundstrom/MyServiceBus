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
    public async Task Publish_should_invoke_all_consumers()
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

    [Fact]
    public async Task Directed_send_should_invoke_all_consumers()
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

        var endpoint = await harness.GetSendEndpoint(new Uri("queue:submit-order"));
        await endpoint.Send(new SubmitOrder(Guid.NewGuid()));

        Assert.Equal(2, harness.Consumed.OfType<SubmitOrder>().Count());
        await harness.Stop();
    }
}
