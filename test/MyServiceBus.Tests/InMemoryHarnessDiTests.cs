using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Linq;
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
    record ConcurrentMessage(int Sequence);

    class CheckOrderConsumer : IConsumer<CheckOrder>
    {
        public Task Consume(ConsumeContext<CheckOrder> context)
            => context.RespondAsync(new OrderStatus(context.Message.OrderId));
    }

    [Fact]
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

        var factory = provider.GetRequiredService<IRequestClientFactory>();
        var address = new Uri($"rabbitmq://localhost/exchange/{EntityNameFormatter.Format(typeof(CheckOrder))}");
        var client = factory.CreateRequestClient<CheckOrder>(address);
        var orderId = Guid.NewGuid();
        var response = await client.GetResponseAsync<OrderStatus>(new CheckOrder(orderId));

        Assert.Equal(orderId, response.Message.OrderId);

        await harness.Stop();
    }

    [Fact]
    public async Task Should_record_concurrent_delivery_deterministically()
    {
        var harness = new InMemoryTestHarness();
        var received = new ConcurrentBag<int>();
        harness.RegisterHandler<ConcurrentMessage>(context =>
        {
            received.Add(context.Message.Sequence);
            return Task.CompletedTask;
        });

        await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(sequence => harness.Publish(new ConcurrentMessage(sequence))));

        Assert.Equal(200, received.Count);
        Assert.Equal(200, received.Distinct().Count());
        Assert.Equal(200, harness.Consumed.OfType<ConcurrentMessage>().Count());
    }
}
