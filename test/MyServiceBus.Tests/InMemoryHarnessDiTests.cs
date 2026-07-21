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

    class ScopedConsumerState
    {
        public readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ConcurrentBag<Guid> InstanceIds = new();
        public int DisposeCount;
    }

    class ScopedAsyncConsumer : IConsumer<SubmitOrder>, IAsyncDisposable
    {
        readonly ScopedConsumerState state;
        readonly Guid instanceId = Guid.NewGuid();

        public ScopedAsyncConsumer(ScopedConsumerState state)
        {
            this.state = state;
        }

        public async Task Consume(ConsumeContext<SubmitOrder> context)
        {
            state.InstanceIds.Add(instanceId);
            await state.Completion.Task;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref state.DisposeCount);
            return ValueTask.CompletedTask;
        }
    }

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
    public async Task Creates_and_disposes_a_consumer_scope_per_delivery()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ScopedConsumerState>();
        services.AddServiceBusTestHarness(x => x.AddConsumer<ScopedAsyncConsumer>());
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        var state = provider.GetRequiredService<ScopedConsumerState>();
        await harness.Start();

        var firstDelivery = harness.Publish(new SubmitOrder(Guid.NewGuid()));
        Assert.False(firstDelivery.IsCompleted);
        Assert.Equal(0, state.DisposeCount);

        state.Completion.SetResult();
        await firstDelivery;
        Assert.Equal(1, state.DisposeCount);

        await harness.Publish(new SubmitOrder(Guid.NewGuid()));
        Assert.Equal(2, state.DisposeCount);
        Assert.Equal(2, state.InstanceIds.Distinct().Count());

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

        await harness.Start();

        await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(sequence => harness.Publish(new ConcurrentMessage(sequence))));

        Assert.Equal(200, received.Count);
        Assert.Equal(200, received.Distinct().Count());
        Assert.Equal(200, harness.Consumed.OfType<ConcurrentMessage>().Count());

        await harness.Stop();
    }

    [Fact]
    public async Task Lifecycle_is_idempotent_and_operations_require_started_state()
    {
        var harness = new InMemoryTestHarness();
        harness.RegisterHandler<SubmitOrder>(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Publish(new SubmitOrder(Guid.NewGuid())));

        await harness.Start();
        await harness.Start();
        await harness.Publish(new SubmitOrder(Guid.NewGuid()));

        await harness.Stop();
        await harness.Stop();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Publish(new SubmitOrder(Guid.NewGuid())));

        await harness.Start();
        await harness.Publish(new SubmitOrder(Guid.NewGuid()));
        Assert.Equal(2, harness.Consumed.OfType<SubmitOrder>().Count());

        await harness.Stop();
    }
}
