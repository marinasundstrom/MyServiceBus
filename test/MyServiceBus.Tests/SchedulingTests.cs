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
        public static TaskCompletionSource<DateTime>? Completed;
        public Task Consume(ConsumeContext<TestMessage> context)
        {
            Received++;
            Completed?.TrySetResult(DateTime.UtcNow);
            return Task.CompletedTask;
        }
    }

    class ImmediateLocalDelayScheduler : ILocalDelayScheduler
    {
        public Task<Guid> Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        {
            _ = callback(cancellationToken);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<Guid> Schedule(TimeSpan delay, Func<CancellationToken, Task> callback, CancellationToken cancellationToken = default)
        {
            _ = callback(cancellationToken);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<bool> Cancel(Guid tokenId) => Task.FromResult(false);
    }

    class ManualLocalDelayScheduler : ILocalDelayScheduler
    {
        readonly Dictionary<Guid, Func<CancellationToken, Task>> jobs = new();

        public Task<Guid> Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            jobs.Add(id, callback);
            return Task.FromResult(id);
        }

        public Task<Guid> Schedule(TimeSpan delay, Func<CancellationToken, Task> callback,
            CancellationToken cancellationToken = default)
            => Schedule(DateTime.UtcNow + delay, callback, cancellationToken);

        public Task<bool> Cancel(Guid tokenId) => Task.FromResult(jobs.Remove(tokenId));

        public async Task Run(Guid tokenId)
        {
            var callback = jobs[tokenId];
            jobs.Remove(tokenId);
            await callback(CancellationToken.None);
        }

        public bool Contains(Guid tokenId) => jobs.ContainsKey(tokenId);
    }

    class RecordingScheduleMessageProvider : IScheduleMessageProvider
    {
        public ScheduleMessageProviderDurability Durability => ScheduleMessageProviderDurability.Durable;
        public bool SupportsCancellation => true;
        public DateTime? ScheduledTime { get; private set; }
        public object? Message { get; private set; }

        public Task<ScheduledMessageHandle> SchedulePublish<T>(DateTime scheduledTime, T message, CancellationToken cancellationToken = default)
            where T : class
        {
            ScheduledTime = scheduledTime;
            Message = message;
            return Task.FromResult(new ScheduledMessageHandle(Guid.NewGuid(), scheduledTime));
        }

        public Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destinationAddress, DateTime scheduledTime, T message, CancellationToken cancellationToken = default)
            where T : class => SchedulePublish(scheduledTime, message, cancellationToken);

        public Task<ScheduleCancellationResult> Cancel(Guid tokenId, CancellationToken cancellationToken = default)
            => Task.FromResult(ScheduleCancellationResult.Cancelled);
    }

    class StubSendContext : IPublishContext
    {
        public string MessageId { get; set; } = string.Empty;
        public Guid? RequestId { get; set; }
        public string RoutingKey { get; set; } = string.Empty;
        public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
        public string? CorrelationId { get; set; }
        public Guid? ConversationId { get; set; }
        public Guid? InitiatorId { get; set; }
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
    public async Task Message_scheduler_delegates_to_message_aware_provider()
    {
        var provider = new RecordingScheduleMessageProvider();
        var scheduler = new MessageScheduler(provider);
        var scheduledTime = DateTime.UtcNow.AddHours(1);
        var message = new TestMessage();

        var handle = await scheduler.SchedulePublish(scheduledTime, message);

        Assert.Equal(ScheduleMessageProviderDurability.Durable, scheduler.Durability);
        Assert.True(scheduler.SupportsCancellation);
        Assert.Equal(scheduledTime, provider.ScheduledTime);
        Assert.Same(message, provider.Message);
        Assert.Equal(scheduledTime, handle.ScheduledTime);
    }

    [Fact]
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
        TestConsumer.Completed = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delay = TimeSpan.FromMilliseconds(100);
        var before = DateTime.UtcNow;
        await scheduler.SchedulePublish(new TestMessage(), delay);
        var consumedAt = await TestConsumer.Completed.Task;
        var tolerance = TimeSpan.FromMilliseconds(20);
        Assert.True(consumedAt - before >= delay - tolerance);
        Assert.Equal(1, TestConsumer.Received);
        TestConsumer.Completed = null;

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
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
    public async Task Custom_scheduler_is_used()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILocalDelayScheduler, ImmediateLocalDelayScheduler>();
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
        TestConsumer.Completed = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delay = TimeSpan.FromSeconds(1);
        await scheduler.SchedulePublish(new TestMessage(), delay);
        await TestConsumer.Completed.Task; // should complete immediately
        Assert.True(TestConsumer.Completed.Task.IsCompleted);
        Assert.Equal(1, TestConsumer.Received);
        TestConsumer.Completed = null;

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Manual_scheduler_controls_publish_and_send_delivery()
    {
        var manual = new ManualLocalDelayScheduler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILocalDelayScheduler>(manual);
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

        var publish = await scheduler.SchedulePublish(new TestMessage(), TimeSpan.FromDays(1));
        var send = await scheduler.ScheduleSend(new Uri("queue:test"), new TestMessage(), TimeSpan.FromDays(1));

        Assert.Equal(0, TestConsumer.Received);
        await manual.Run(publish.TokenId);
        Assert.Equal(1, TestConsumer.Received);
        await manual.Run(send.TokenId);
        Assert.Equal(2, TestConsumer.Received);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
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

    [Fact]
    public async Task Cancel_prevents_scheduled_publish()
    {
        var manual = new ManualLocalDelayScheduler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILocalDelayScheduler>(manual);
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
        var delay = TimeSpan.FromDays(1);
        var handle = await scheduler.SchedulePublish(new TestMessage(), delay);
        await scheduler.CancelScheduledPublish(handle);
        Assert.False(manual.Contains(handle.TokenId));
        Assert.Equal(0, TestConsumer.Received);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task In_memory_scheduler_reports_scheduled_work_lifecycle()
    {
        var manual = new ManualLocalDelayScheduler();
        var observer = new RecordingScheduledWorkObserver();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILocalDelayScheduler>(manual);
        services.AddSingleton<IScheduledWorkObserver>(observer);
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<TestConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);
        var scheduler = provider.GetRequiredService<IMessageScheduler>();
        var handle = await scheduler.SchedulePublish(new TestMessage(), TimeSpan.FromDays(1));

        Assert.Single(observer.States);
        Assert.Equal(ScheduledWorkStatus.Pending, observer.States[0].Status);
        await manual.Run(handle.TokenId);
        Assert.Equal([
            ScheduledWorkStatus.Pending,
            ScheduledWorkStatus.Running,
            ScheduledWorkStatus.Completed], observer.States.Select(state => state.Status));

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task In_memory_scheduled_work_is_shared_across_application_scopes()
    {
        var manual = new ManualLocalDelayScheduler();
        var observer = new RecordingScheduledWorkObserver();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILocalDelayScheduler>(manual);
        services.AddSingleton<IScheduledWorkObserver>(observer);
        services.AddServiceBus(cfg => cfg.UsingMediator());

        await using var provider = services.BuildServiceProvider();
        ScheduledMessageHandle handle;
        await using (var schedulingScope = provider.CreateAsyncScope())
            handle = await schedulingScope.ServiceProvider.GetRequiredService<IMessageScheduler>()
                .SchedulePublish(new TestMessage(), TimeSpan.FromDays(1));

        Assert.Single(await provider.GetRequiredService<IScheduledWorkSource>().GetSnapshotAsync(100));
        await using (var cancellationScope = provider.CreateAsyncScope())
            await cancellationScope.ServiceProvider.GetRequiredService<IMessageScheduler>()
                .CancelScheduledPublish(handle);

        Assert.Equal(ScheduledWorkStatus.Cancelled, observer.States[^1].Status);
        Assert.Empty(await provider.GetRequiredService<IScheduledWorkSource>().GetSnapshotAsync(100));
    }

    private sealed class RecordingScheduledWorkObserver : IScheduledWorkObserver
    {
        public List<ScheduledWorkState> States { get; } = [];

        public void Observe(ScheduledWorkState state) => States.Add(state);
    }
}
