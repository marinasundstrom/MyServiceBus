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
}
