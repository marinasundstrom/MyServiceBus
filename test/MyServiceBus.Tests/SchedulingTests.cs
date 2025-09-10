using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Sdk;

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

        var bus = provider.GetRequiredService<IMessageBus>();
        TestConsumer.Received = 0;
        var delay = TimeSpan.FromMilliseconds(100);
        var sw = Stopwatch.StartNew();
        await bus.Publish(new TestMessage(), ctx => ctx.SetScheduledEnqueueTime(delay));
        sw.Stop();

        Assert.True(sw.Elapsed >= delay);
        Assert.Equal(1, TestConsumer.Received);

        await hosted.StopAsync(CancellationToken.None);
    }
}
