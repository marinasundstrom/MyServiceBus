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
        public static TaskCompletionSource<bool>? Completed;
        public Task Consume(ConsumeContext<TestMessage> context)
        {
            Received++;
            Completed?.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    class ImmediateMessageScheduler : IMessageScheduler
    {
        readonly IPublishEndpoint _publishEndpoint;
        readonly ISendEndpointProvider _sendEndpointProvider;

        public ImmediateMessageScheduler(IPublishEndpoint publishEndpoint, ISendEndpointProvider sendEndpointProvider)
        {
            _publishEndpoint = publishEndpoint;
            _sendEndpointProvider = sendEndpointProvider;
        }

        public Task SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
            => _publishEndpoint.Publish(message, cancellationToken: cancellationToken);

        public Task SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
            => _publishEndpoint.Publish(message, cancellationToken: cancellationToken);

        public async Task ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
        {
            var endpoint = await _sendEndpointProvider.GetSendEndpoint(destination);
            await endpoint.Send(message, cancellationToken: cancellationToken);
        }

        public Task ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
            => ScheduleSend(destination, message, DateTime.UtcNow, cancellationToken);
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
    public async Task Custom_scheduler_is_used()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<TestConsumer>();
        });
        services.AddScoped<IMessageScheduler, ImmediateMessageScheduler>();

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
