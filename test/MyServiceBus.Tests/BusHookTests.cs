using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MyServiceBus;
using MyServiceBus.Inspection;
using MyServiceBus.Monitoring;
using Shouldly;

public class BusHookTests
{
    [Fact]
    public async Task Registered_hooks_observe_lifecycle_and_message_operations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(configurator =>
        {
            configurator.UsingMediator();
            configurator.AddHook<RecordingHook>();
            configurator.AddConsumer<TestConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        await provider.GetRequiredService<IMessageBus>().Publish(new TestMessage("hello"));
        await hostedService.StopAsync(CancellationToken.None);

        var hook = provider.GetServices<IBusHook>().OfType<RecordingHook>().Single();
        hook.Events.OfType<BusLifecycleHookEvent>().Select(busEvent => busEvent.State)
            .ShouldBe(["started", "stopped"]);
        hook.Events.OfType<MessageOperationHookEvent>().Select(busEvent => busEvent.Kind)
            .ShouldContain("published");
        hook.Events.OfType<MessageOperationHookEvent>().Select(busEvent => busEvent.Kind)
            .ShouldContain("consumed");
        var published = hook.Events.OfType<MessageOperationHookEvent>()
            .Single(busEvent => busEvent.Kind == "published");
        var consumed = hook.Events.OfType<MessageOperationHookEvent>()
            .Single(busEvent => busEvent.Kind == "consumed");
        published.MessageId.ShouldNotBeNullOrWhiteSpace();
        consumed.MessageId.ShouldBe(published.MessageId);
    }

    [Fact]
    public async Task Hook_failures_do_not_change_message_outcomes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(configurator =>
        {
            configurator.UsingMediator();
            configurator.AddHook<ThrowingHook>();
        });

        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        await provider.GetRequiredService<IMessageBus>().Publish(new TestMessage("hello"));
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Consumer_outgoing_operations_report_the_consumed_message_as_their_cause()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(configurator =>
        {
            configurator.UsingMediator();
            configurator.AddHook<RecordingHook>();
            configurator.AddConsumer<ReactingConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);
        await provider.GetRequiredService<IMessageBus>().Publish(new TestMessage("react"));

        var operations = provider.GetServices<IBusHook>().OfType<RecordingHook>().Single()
            .Events.OfType<MessageOperationHookEvent>().ToArray();
        var consumed = operations.Single(operation =>
            operation.Kind == "consumed" && operation.MessageType == typeof(TestMessage).FullName);
        var reaction = operations.Single(operation =>
            operation.Kind == "published" && operation.MessageType == typeof(ReactionMessage).FullName);
        reaction.CausationMessageId.ShouldBe(consumed.MessageId);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Retry_hooks_report_attempts_and_exhaustion()
    {
        RetryingConsumer.Attempts = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(configurator =>
        {
            configurator.UsingMediator();
            configurator.AddHook<RecordingHook>();
            configurator.AddConsumer<RetryingConsumer, TestMessage>(pipe => pipe.UseRetry(1));
        });

        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<IMessageBus>().Publish(new TestMessage("retry")));

        var operations = provider.GetServices<IBusHook>()
            .OfType<RecordingHook>()
            .Single()
            .Events
            .OfType<MessageOperationHookEvent>()
            .ToArray();
        var attempted = operations.Single(operation => operation.Kind == "retry_attempted");
        attempted.RetryAttempt.ShouldBe(1);
        attempted.RetryLimit.ShouldBe(1);
        var exhausted = operations.Single(operation => operation.Kind == "retry_exhausted");
        exhausted.RetryAttempt.ShouldBe(2);
        exhausted.RetryLimit.ShouldBe(1);
        RetryingConsumer.Attempts.ShouldBe(2);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Monitoring_exporter_can_be_resolved_as_a_hook_without_a_bus_dependency_cycle()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(configurator => configurator.UsingMediator());
        services.AddServiceBusMonitoring(options =>
        {
            options.ServiceAddress = new Uri("http://localhost:5310");
            options.ApplicationName = "tests";
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IMessageBus>().ShouldNotBeNull();
        var hookExporter = provider.GetServices<IBusHook>().OfType<MonitoringExporter>().ShouldHaveSingleItem();
        var hostedExporter = provider.GetServices<IHostedService>().OfType<MonitoringExporter>().ShouldHaveSingleItem();
        hookExporter.ShouldBeSameAs(hostedExporter);
        provider.GetServices<IScheduledWorkObserver>().ShouldContain(hookExporter);
    }

    [Fact]
    public async Task Monitoring_exporter_drains_events_that_arrive_after_an_empty_interval()
    {
        var handler = new RecordingHttpHandler();
        var services = new ServiceCollection()
            .AddSingleton<IBusInspectionProvider>(new StubInspectionProvider());
        await using var provider = services.BuildServiceProvider();
        var options = new MonitoringExporterOptions
        {
            ServiceAddress = new Uri("http://monitoring.test"),
            ApplicationName = "tests",
            CaptureProfile = MonitoringCaptureProfile.Development,
            ExportInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatInterval = TimeSpan.FromMinutes(1)
        };
        var exporter = new MonitoringExporter(
            new HttpClient(handler) { BaseAddress = options.ServiceAddress },
            provider,
            options,
            NullLogger<MonitoringExporter>.Instance);

        await exporter.StartAsync(CancellationToken.None);
        await Task.Delay(options.ExportInterval * 3);
        exporter.Handle(MessageOperationHookEvent.Create(
            "published",
            true,
            typeof(TestMessage).FullName!,
            MessageUrn.For(typeof(TestMessage)),
            null,
            "loopback://test-message",
            TimeSpan.Zero,
            exception: new InvalidOperationException("customer 42"),
            correlationId: "correlation-1",
            conversationId: "conversation-1",
            messageId: "message-1",
            causationMessageId: "trigger-1",
            requestId: "request-1",
            responseAddress: "loopback://responses",
            messageIntent: "Reply"));

        var batchJson = await handler.BatchReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        batchJson.ShouldContain("\"kind\":\"published\"");
        batchJson.ShouldContain("\"messageId\":\"message-1\"");
        batchJson.ShouldContain("\"causationMessageId\":\"trigger-1\"");
        batchJson.ShouldContain("\"requestId\":\"request-1\"");
        batchJson.ShouldContain("\"responseAddress\":\"loopback://responses\"");
        batchJson.ShouldContain("\"messageIntent\":\"Reply\"");
        await exporter.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Production_capture_profile_excludes_sensitive_message_metadata()
    {
        var handler = new RecordingHttpHandler();
        var services = new ServiceCollection()
            .AddSingleton<IBusInspectionProvider>(new StubInspectionProvider());
        await using var provider = services.BuildServiceProvider();
        var options = new MonitoringExporterOptions
        {
            ServiceAddress = new Uri("http://monitoring.test"),
            ApplicationName = "production-tests",
            CaptureProfile = MonitoringCaptureProfile.Production,
            ExportInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatInterval = TimeSpan.FromMinutes(1)
        };
        var exporter = new MonitoringExporter(
            new HttpClient(handler) { BaseAddress = options.ServiceAddress },
            provider,
            options,
            NullLogger<MonitoringExporter>.Instance);

        await exporter.StartAsync(CancellationToken.None);
        exporter.Handle(MessageOperationHookEvent.Create(
            "sent",
            false,
            typeof(TestMessage).FullName!,
            MessageUrn.For(typeof(TestMessage)),
            "orders",
            "loopback://orders",
            TimeSpan.FromMilliseconds(4),
            new InvalidOperationException("customer 42"),
            "correlation-1",
            "conversation-1",
            messageId: "message-1",
            causationMessageId: "trigger-1",
            requestId: "request-1",
            responseAddress: "loopback://responses",
            messageIntent: "Send"));

        var batchJson = await handler.BatchReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        batchJson.ShouldContain("\"messageUrn\"");
        batchJson.ShouldContain("\"durationMs\"");
        batchJson.ShouldContain("\"exceptionType\"");
        batchJson.ShouldNotContain("customer 42");
        batchJson.ShouldNotContain("loopback://orders");
        batchJson.ShouldNotContain("correlation-1");
        batchJson.ShouldNotContain("conversation-1");
        batchJson.ShouldNotContain("message-1");
        batchJson.ShouldNotContain("trigger-1");
        batchJson.ShouldNotContain("request-1");
        batchJson.ShouldNotContain("loopback://responses");
        await exporter.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Monitoring_exporter_maps_bounded_outbox_dispatch_properties()
    {
        var handler = new RecordingHttpHandler();
        var services = new ServiceCollection()
            .AddSingleton<IBusInspectionProvider>(new StubInspectionProvider());
        await using var provider = services.BuildServiceProvider();
        var options = new MonitoringExporterOptions
        {
            ServiceAddress = new Uri("http://monitoring.test"),
            ApplicationName = "dispatcher-tests",
            ExportInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatInterval = TimeSpan.FromMinutes(1)
        };
        var exporter = new MonitoringExporter(
            new HttpClient(handler) { BaseAddress = options.ServiceAddress },
            provider,
            options,
            NullLogger<MonitoringExporter>.Instance);

        await exporter.StartAsync(CancellationToken.None);
        exporter.Handle(new OutboxDeliveryHookEvent(
            DateTimeOffset.UtcNow,
            "orders-service",
            "orders-dispatcher-a",
            true,
            12.5,
            8,
            7,
            1,
            0,
            11,
            2,
            3,
            40,
            1,
            4,
            2_500,
            null));

        var batchJson = await handler.BatchReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        batchJson.ShouldContain("\"kind\":\"outbox_dispatch_cycle\"");
        batchJson.ShouldContain("\"service_name\":\"orders-service\"");
        batchJson.ShouldContain("\"batch_dispatched\":\"7\"");
        batchJson.ShouldContain("\"pending\":\"11\"");
        batchJson.ShouldNotContain("message_id");
        await exporter.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Monitoring_exporter_sends_scheduled_work_without_message_bodies()
    {
        var handler = new RecordingHttpHandler();
        var services = new ServiceCollection()
            .AddSingleton<IBusInspectionProvider>(new StubInspectionProvider());
        await using var provider = services.BuildServiceProvider();
        var options = new MonitoringExporterOptions
        {
            ServiceAddress = new Uri("http://monitoring.test"),
            ApplicationName = "scheduler-tests",
            ExportInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatInterval = TimeSpan.FromMinutes(1)
        };
        var exporter = new MonitoringExporter(
            new HttpClient(handler) { BaseAddress = options.ServiceAddress },
            provider,
            options,
            NullLogger<MonitoringExporter>.Instance);

        exporter.Observe(new ScheduledWorkState(
            Guid.NewGuid(), "InMemory", SchedulingDurability.Volatile, "Message",
            typeof(TestMessage).FullName!, "Publish", null, DateTimeOffset.UtcNow.AddMinutes(1),
            ScheduledWorkStatus.Pending, "Pending", 0, DateTimeOffset.UtcNow));
        await exporter.StartAsync(CancellationToken.None);

        var json = await handler.ScheduledWorkReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        json.ShouldContain("\"status\":\"Pending\"");
        json.ShouldContain("\"messageType\":");
        json.ShouldNotContain("secret-body");
        await exporter.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Monitoring_exporter_restores_scheduled_work_from_authoritative_source()
    {
        var state = new ScheduledWorkState(
            Guid.NewGuid(), "PostgreSQL", SchedulingDurability.Durable, "Message",
            typeof(TestMessage).FullName!, "Publish", null, DateTimeOffset.UtcNow.AddMinutes(1),
            ScheduledWorkStatus.Pending, "Pending", 0, DateTimeOffset.UtcNow);
        var handler = new RecordingHttpHandler();
        var services = new ServiceCollection()
            .AddSingleton<IBusInspectionProvider>(new StubInspectionProvider())
            .AddSingleton<IScheduledWorkSource>(new StubScheduledWorkSource(state));
        await using var provider = services.BuildServiceProvider();
        var options = new MonitoringExporterOptions
        {
            ServiceAddress = new Uri("http://monitoring.test"),
            ApplicationName = "scheduler-source-tests",
            ExportInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatInterval = TimeSpan.FromMinutes(1)
        };
        var exporter = new MonitoringExporter(
            new HttpClient(handler) { BaseAddress = options.ServiceAddress },
            provider,
            options,
            NullLogger<MonitoringExporter>.Instance);

        await exporter.StartAsync(CancellationToken.None);
        var json = await handler.ScheduledWorkReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        json.ShouldContain(state.TokenId.ToString("D"));
        json.ShouldContain("\"provider\":\"PostgreSQL\"");
        await exporter.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Monitoring_exporter_sends_jobs_with_bounded_attempts_without_payloads()
    {
        var now = DateTimeOffset.UtcNow;
        var occurrenceId = Guid.NewGuid();
        var state = new JobState(
            Guid.NewGuid(), "invoice-export", JobStatus.Faulted, "in-memory",
            SchedulingDurability.Volatile, SchedulingPlacement.ProcessLocal,
            now.AddSeconds(-2), null, now.AddSeconds(-1), now, new JobProgress(4, 10), occurrenceId, now);
        var attempt = new JobAttemptState(
            Guid.NewGuid(), state.JobId, 0, JobAttemptStatus.Faulted,
            now.AddSeconds(-1), now, "System.InvalidOperationException", "secret-body");
        var handler = new RecordingHttpHandler();
        var services = new ServiceCollection()
            .AddSingleton<IBusInspectionProvider>(new StubInspectionProvider())
            .AddSingleton<IJobSource>(new StubJobSource(state, attempt));
        await using var provider = services.BuildServiceProvider();
        var options = new MonitoringExporterOptions
        {
            ServiceAddress = new Uri("http://monitoring.test"),
            ApplicationName = "job-tests",
            ExportInterval = TimeSpan.FromMilliseconds(20),
            HeartbeatInterval = TimeSpan.FromMinutes(1)
        };
        var exporter = new MonitoringExporter(
            new HttpClient(handler) { BaseAddress = options.ServiceAddress },
            provider,
            options,
            NullLogger<MonitoringExporter>.Instance);

        await exporter.StartAsync(CancellationToken.None);
        var json = await handler.JobsReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        json.ShouldContain(state.JobId.ToString("D"));
        json.ShouldContain(occurrenceId.ToString("D"));
        json.ShouldContain("System.InvalidOperationException");
        json.ShouldNotContain("secret-body");
        await exporter.StopAsync(CancellationToken.None);
    }

    public sealed record TestMessage(string Value);
    public sealed record ReactionMessage(string Value);

    public sealed class TestConsumer : IConsumer<TestMessage>
    {
        public Task Consume(ConsumeContext<TestMessage> context) => Task.CompletedTask;
    }

    public sealed class ReactingConsumer : IConsumer<TestMessage>
    {
        public Task Consume(ConsumeContext<TestMessage> context) =>
            context.Publish(new ReactionMessage(context.Message.Value));
    }

    public sealed class RetryingConsumer : IConsumer<TestMessage>
    {
        public static int Attempts { get; set; }

        public Task Consume(ConsumeContext<TestMessage> context)
        {
            Attempts++;
            throw new InvalidOperationException("retry failure");
        }
    }

    public sealed class RecordingHook : IBusHook
    {
        public ConcurrentQueue<BusHookEvent> Events { get; } = new();

        public void Handle(BusHookEvent busEvent) => Events.Enqueue(busEvent);
    }

    public sealed class ThrowingHook : IBusHook
    {
        public void Handle(BusHookEvent busEvent) => throw new InvalidOperationException("Hook failure");
    }

    private sealed class StubInspectionProvider : IBusInspectionProvider
    {
        public BusInspectionSnapshot GetSnapshot() => new(
            "mediator",
            new Uri("loopback://localhost/"),
            DateTimeOffset.UtcNow,
            [],
            [],
            []);
    }

    private sealed class StubScheduledWorkSource(ScheduledWorkState state) : IScheduledWorkSource
    {
        public string Provider => "PostgreSQL";
        public bool Authoritative => true;

        public Task<IReadOnlyList<ScheduledWorkState>> GetSnapshotAsync(
            int maximumCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScheduledWorkState>>([state]);
    }

    private sealed class StubJobSource(JobState state, JobAttemptState attempt) : IJobSource
    {
        public string Provider => "in-memory";
        public bool Authoritative => true;

        public Task<IReadOnlyList<JobState>> GetSnapshotAsync(
            int maximumCount,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<JobState>>([state]);

        public Task<IReadOnlyList<JobAttemptState>> GetAttemptsAsync(
            Guid jobId,
            int maximumCount,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<JobAttemptState>>([attempt]);
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public TaskCompletionSource<string> BatchReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> ScheduledWorkReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> JobsReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri?.AbsolutePath.EndsWith("observations:batch", StringComparison.Ordinal) == true)
                BatchReceived.TrySetResult(json);
            if (request.RequestUri?.AbsolutePath.EndsWith("scheduled-work", StringComparison.Ordinal) == true)
                ScheduledWorkReceived.TrySetResult(json);
            if (request.RequestUri?.AbsolutePath.EndsWith("/jobs", StringComparison.Ordinal) == true)
                JobsReceived.TrySetResult(json);

            return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
        }
    }
}
