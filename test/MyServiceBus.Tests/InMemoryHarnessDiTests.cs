using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class InMemoryHarnessDiTests
{
    record SubmitOrder(Guid OrderId);

    class SubmitOrderConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    record CheckOrder(Guid OrderId);
    record OrderStatus(Guid OrderId);

    class CheckOrderConsumer : IConsumer<CheckOrder>
    {
        public Task Consume(ConsumeContext<CheckOrder> context)
            => context.RespondAsync(new OrderStatus(context.Message.OrderId));
    }

    [Fact]
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException), typeof(TrueException))]
    public async Task Should_resolve_consumer_from_di()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(x =>
        {
            x.AddConsumer<SubmitOrderConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();

        await harness.Start();

        await harness.Publish(new SubmitOrder(Guid.NewGuid()));

        Assert.True(harness.WasConsumed<SubmitOrder>());

        await harness.Stop();
    }

    [Fact]
    [Throws(typeof(InvalidOperationException), typeof(RequestFaultException))]
    public async Task Should_resolve_request_client()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(x =>
        {
            x.AddConsumer<CheckOrderConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();

        await harness.Start();

        var client = provider.GetRequiredService<IRequestClient<CheckOrder>>();
        var orderId = Guid.NewGuid();
        var response = await client.GetResponseAsync<OrderStatus>(new CheckOrder(orderId));

        Assert.Equal(orderId, response.Message.OrderId);

        await harness.Stop();
    }

    [Fact]
    [Throws(typeof(InvalidOperationException), typeof(RequestFaultException), typeof(UriFormatException))]
    public async Task Should_create_request_client_from_factory()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(x =>
        {
            x.AddConsumer<CheckOrderConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();

        await harness.Start();

        var factory = provider.GetRequiredService<IScopedClientFactory>();
        var address = new Uri($"rabbitmq://localhost/exchange/{NamingConventions.GetExchangeName(typeof(CheckOrder))}");
        var client = factory.CreateRequestClient<CheckOrder>(address);
        var orderId = Guid.NewGuid();
        var response = await client.GetResponseAsync<OrderStatus>(new CheckOrder(orderId));

        Assert.Equal(orderId, response.Message.OrderId);

        await harness.Stop();
    }
}
