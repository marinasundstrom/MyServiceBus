using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using MyServiceBus.Serialization;

public class TelemetryDiWiringTests
{
    class TestMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    class TestConsumer : IConsumer<TestMessage>
    {
        public static TaskCompletionSource<TestMessage> Received { get; set; } = new();

        public Task Consume(ConsumeContext<TestMessage> context)
        {
            Received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AddServiceBus_with_mediator_emits_send_and_consume_activities()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MyServiceBus",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };

        var startedActivities = new List<string>();
        listener.ActivityStarted = activity => startedActivities.Add(activity.OperationName);
        ActivitySource.AddActivityListener(listener);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<TestConsumer>();
        });

        using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        TestConsumer.Received = new TaskCompletionSource<TestMessage>();

        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.Publish(new TestMessage { Value = "hello" });
        await TestConsumer.Received.Task;

        Assert.Contains("send", startedActivities);
        Assert.Contains("consume", startedActivities);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RabbitMq_registration_resolves_bus_with_di_pipes_and_publish_pipe_injects_traceparent()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MyServiceBus",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingRabbitMq((_, rabbit) => rabbit.Host("localhost"));
        });

        using var provider = services.BuildServiceProvider();

        var bus = Assert.IsType<MessageBus>(provider.GetRequiredService<IMessageBus>());
        var sendPipe = provider.GetRequiredService<ISendPipe>();
        var publishPipe = provider.GetRequiredService<IPublishPipe>();

        var sendPipeField = typeof(MessageBus).GetField("_sendPipe", BindingFlags.Instance | BindingFlags.NonPublic);
        var publishPipeField = typeof(MessageBus).GetField("_publishPipe", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Same(sendPipe, sendPipeField!.GetValue(bus));
        Assert.Same(publishPipe, publishPipeField!.GetValue(bus));

        var contextFactory = provider.GetRequiredService<IPublishContextFactory>();
        var context = contextFactory.Create(
            MessageTypeCache.GetMessageTypes(typeof(TestMessage)),
            new EnvelopeMessageSerializer(),
            CancellationToken.None);

        await publishPipe.Send(context);

        Assert.True(context.Headers.ContainsKey(MyServiceBusDiagnostics.TraceParent));
    }
}
