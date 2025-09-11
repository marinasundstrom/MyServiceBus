using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Sdk;
using MyServiceBus;

namespace MyServiceBus.Tests;

public class SchedulingTests
{
    class TestMessage { }

    class TestConsumer : IConsumer<TestMessage>
    {
        public static int Received;
        public Task Consume(ConsumeContext<TestMessage> context)
        {
            Received++;
            return Task.CompletedTask;
        }
    }

    class ImmediateJobScheduler : IJobScheduler
    {
        public Task Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
            => callback(cancellationToken);

        public Task Schedule(TimeSpan delay, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
            => callback(cancellationToken);
    }

    class StubSendContext : IPublishContext
    {
        public string MessageId { get; set; } = string.Empty;
        public string RoutingKey { get; set; } = string.Empty;
        public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
        public string? CorrelationId { get; set; }
        public Uri? ResponseAddress { get; set; }
        public Uri? FaultAddress { get; set; }
        public DateTime? ScheduledEnqueueTime { get; set; }
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    class StubPublishEndpoint : IPublishEndpoint
    {
        public StubSendContext? Context;

        public Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
            => Publish((T)message, contextCallback, cancellationToken);

        public Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
        {
            var ctx = new StubSendContext();
            contextCallback?.Invoke(ctx);
            Context = ctx;
            return Task.CompletedTask;
        }
    }

    class StubSendEndpoint : ISendEndpoint
    {
        public StubSendContext? Context;

        public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
        {
            var ctx = new StubSendContext();
            contextCallback?.Invoke(ctx);
            Context = ctx;
            return Task.CompletedTask;
        }

        public Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
            => Send((T)message, contextCallback, cancellationToken);
    }

    [Fact]
    [Throws(typeof(TrueException))]
    public async Task SchedulePublish_delays_message()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<TestConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        var scheduler = provider.GetRequiredService<IMessageScheduler>();
        TestConsumer.Received = 0;
        var delay = TimeSpan.FromMilliseconds(100);
        var sw = Stopwatch.StartNew();
        await scheduler.SchedulePublish(new TestMessage(), delay);
        sw.Stop();

        var tolerance = TimeSpan.FromMilliseconds(20);
        Assert.True(sw.Elapsed >= delay - tolerance);
        Assert.Equal(1, TestConsumer.Received);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    [Throws(typeof(TrueException))]
    public async Task Publish_context_delays_message()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<TestConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
        TestConsumer.Received = 0;
        var delay = TimeSpan.FromMilliseconds(100);
        var sw = Stopwatch.StartNew();
        await publishEndpoint.Publish(new TestMessage(), ctx => ctx.SetScheduledEnqueueTime(delay));
        sw.Stop();

        var tolerance = TimeSpan.FromMilliseconds(20);
        Assert.True(sw.Elapsed >= delay - tolerance);
        Assert.Equal(1, TestConsumer.Received);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    [Throws(typeof(TrueException))]
    public async Task Custom_scheduler_is_used()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJobScheduler, ImmediateJobScheduler>();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<TestConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        var scheduler = provider.GetRequiredService<IMessageScheduler>();
        TestConsumer.Received = 0;
        var delay = TimeSpan.FromSeconds(1);
        var sw = Stopwatch.StartNew();
        await scheduler.SchedulePublish(new TestMessage(), delay);
        sw.Stop();

        Assert.True(sw.Elapsed < delay);
        Assert.Equal(1, TestConsumer.Received);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    [Throws(typeof(NotNullException))]
    public async Task Publish_extension_sets_scheduled_time()
    {
        var endpoint = new StubPublishEndpoint();
        var delay = TimeSpan.FromMilliseconds(100);
        var before = DateTime.UtcNow;
        await endpoint.SchedulePublish(new TestMessage(), delay);

        Assert.NotNull(endpoint.Context);
        var scheduled = endpoint.Context!.ScheduledEnqueueTime;
        var tolerance = TimeSpan.FromMilliseconds(50);
        Assert.InRange(scheduled!.Value, before + delay - tolerance, before + delay + tolerance);
    }

    [Fact]
    [Throws(typeof(NotNullException))]
    public async Task Send_extension_sets_scheduled_time()
    {
        var endpoint = new StubSendEndpoint();
        var delay = TimeSpan.FromMilliseconds(100);
        var before = DateTime.UtcNow;
        await endpoint.ScheduleSend(new TestMessage(), delay);

        Assert.NotNull(endpoint.Context);
        var scheduled = endpoint.Context!.ScheduledEnqueueTime;
        var tolerance = TimeSpan.FromMilliseconds(50);
        Assert.InRange(scheduled!.Value, before + delay - tolerance, before + delay + tolerance);
    }
}
