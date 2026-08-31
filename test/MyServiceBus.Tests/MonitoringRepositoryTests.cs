using MyServiceBus.Inspection;
using MyServiceBus;
using MyServiceBus.Monitoring;
using MyServiceBus.Monitoring.Server;
using Shouldly;

public class MonitoringRepositoryTests
{
    [Fact]
    public void Dashboard_summary_is_explicit_when_no_monitoring_data_is_available()
    {
        var summary = new MonitoringRepository().GetDashboardSummary(60, DateTimeOffset.UtcNow);

        summary.FailureCount.ShouldBe(0);
        summary.MonitoredApplicationCount.ShouldBe(0);
        summary.StaleApplicationCount.ShouldBe(0);
        summary.LatestMonitoringUpdateAtUtc.ShouldBeNull();
        summary.LatestObservationAtUtc.ShouldBeNull();
        summary.Complete.ShouldBeTrue();
    }

    [Fact]
    public void Dashboard_summary_uses_a_rolling_failure_window_and_reports_coverage()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce"));
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-5), "consume_faulted", false, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 12, typeof(InvalidOperationException).FullName, "failed", null, null, null, null),
            new MonitoringObservation(
                2, now.AddSeconds(-4), "retry_attempted", false, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 0, typeof(InvalidOperationException).FullName, "retry", null, null, null, null, 1, 3),
            new MonitoringObservation(
                3, now.AddSeconds(-3), "outbox_dispatch_cycle", false, null, null,
                null, null, 5, "database", "unavailable", null, null, null, null, Properties: new Dictionary<string, string>
                {
                    ["service_name"] = "orders-outbox",
                    ["owner_id"] = "orders-1"
                })))
            .ShouldBeTrue();
        repository.UpsertJobs(new MonitoringJobSnapshot(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "bus",
            now,
            [
                new MonitoringJobItem(
                    "job-1", "ImportOrders", "Running", "in-memory", "Volatile", "ProcessLocal",
                    now, null, now, null, null, null, null, now, []),
                new MonitoringJobItem(
                    "job-2", "ExportOrders", "Faulted", "in-memory", "Volatile", "ProcessLocal",
                    now, null, now, now, null, null, null, now, [])
            ])).ShouldBeTrue();

        var active = repository.GetDashboardSummary(60, now);
        active.WindowSeconds.ShouldBe(60);
        active.WindowStartUtc.ShouldBe(now.AddSeconds(-60));
        active.CapturedAtUtc.ShouldBe(now);
        active.FailureCount.ShouldBe(1);
        active.RetryCount.ShouldBe(1);
        active.AffectedApplicationCount.ShouldBe(1);
        active.UnhealthyOutboxDispatcherCount.ShouldBe(1);
        active.FaultedTrackedJobCount.ShouldBe(1);
        active.RunningTrackedJobCount.ShouldBe(1);
        active.MonitoredApplicationCount.ShouldBe(1);
        active.StaleApplicationCount.ShouldBe(0);
        active.LatestMonitoringUpdateAtUtc.ShouldBe(now);
        active.LatestObservationAtUtc.ShouldBe(now.AddSeconds(-3));
        active.Complete.ShouldBeTrue();

        var recovered = repository.GetDashboardSummary(60, now.AddSeconds(61));
        recovered.FailureCount.ShouldBe(0);
        recovered.RetryCount.ShouldBe(0);
        recovered.AffectedApplicationCount.ShouldBe(0);
    }

    [Fact]
    public void Repository_keeps_job_freshness_and_instance_availability_explicit()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("orders", "replica-1", now, "commerce"));
        var item = new MonitoringJobItem(
            Guid.NewGuid().ToString("D"), "invoice-export", "Running", "in-memory", "Volatile",
            "ProcessLocal", now.AddMinutes(-1), null, now.AddSeconds(-5), null, 4, 10,
            Guid.NewGuid().ToString("D"), now, []);
        repository.UpsertJobs(new MonitoringJobSnapshot(
            MonitoringProtocol.Version, "orders", "replica-1", "bus", now, [item])).ShouldBeTrue();

        var summary = repository.GetJobs("orders", "running", now).ShouldHaveSingleItem();
        summary.InstanceOnline.ShouldBeTrue();
        summary.CapturedAtUtc.ShouldBe(now);
        summary.Job.ShouldBe(item);
    }

    [Fact]
    public void Repository_keeps_recurring_definitions_separate_from_scheduled_occurrences()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce"));
        var item = new MonitoringRecurringJobItem(
            Guid.NewGuid().ToString(), "daily-summary", "reporting", 2,
            "MyServiceBus.Durable", "Durable", "Embedded", "Every 01:00:00",
            "CreateSummary", "Active", now.AddHours(1), now);

        repository.UpsertRecurringJobs(new MonitoringRecurringJobSnapshot(
            MonitoringProtocol.Version, "orders", "orders-1", "bus", now, [item])).ShouldBeTrue();

        var summary = repository.GetRecurringJobs("orders", "active", now).ShouldHaveSingleItem();
        summary.Job.ScheduleId.ShouldBe("daily-summary");
        summary.InstanceOnline.ShouldBeTrue();
        repository.GetScheduledWork(null, null, now).ShouldBeEmpty();
    }

    [Fact]
    public void Repository_replaces_scheduled_work_snapshots_and_reports_participant_health()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce"));
        var item = new MonitoringScheduledWorkItem(
            Guid.NewGuid().ToString(), "InMemory", "Volatile", "Message", "SubmitOrder", "Publish", null,
            now.AddMinutes(2), "Pending", "Pending", 0, now);

        repository.UpsertScheduledWork(new MonitoringScheduledWorkSnapshot(
            MonitoringProtocol.Version, "orders", "orders-1", "bus", now, [item])).ShouldBeTrue();

        var summary = repository.GetScheduledWork("orders", "pending", now).ShouldHaveSingleItem();
        summary.ApplicationName.ShouldBe("orders");
        summary.InstanceOnline.ShouldBeTrue();
        summary.Work.MessageType.ShouldBe("SubmitOrder");

        repository.UpsertScheduledWork(new MonitoringScheduledWorkSnapshot(
            MonitoringProtocol.Version, "orders", "orders-1", "bus", now.AddSeconds(1), [])).ShouldBeTrue();
        repository.GetScheduledWork(null, null, now).ShouldBeEmpty();
    }

    [Fact]
    public void Repository_registers_instances_and_deduplicates_observation_batches()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(new MonitoringMetadata(
            MonitoringProtocol.Version,
            "orders",
            "replica-1",
            "1.0.0",
            "dotnet",
            "1.0.0",
            "bus",
            now,
            now,
            new BusInspectionSnapshot(
                "mediator",
                new Uri("loopback://localhost/"),
                now,
                [],
                [],
                []),
            new Dictionary<string, string> { ["group"] = "commerce" }));

        var observation = new MonitoringObservation(
            1,
            now,
            "published",
            true,
            typeof(TestMessage).FullName,
            MessageUrn.For(typeof(TestMessage)),
            null,
            "loopback://test-message",
            1.5,
            null,
            null,
            null,
            null,
            null,
            null);
        var batch = new MonitoringObservationBatch(
            MonitoringProtocol.Version,
            "orders",
            "replica-1",
            "bus",
            "batch-1",
            1,
            1,
            0,
            now,
            [observation]);

        repository.RecordBatch(batch).ShouldBeTrue();
        repository.RecordBatch(batch).ShouldBeTrue();

        var application = repository.GetApplications(now).ShouldHaveSingleItem();
        application.ApplicationName.ShouldBe("orders");
        application.OnlineInstances.ShouldBe(1);
        application.Totals.Published.ShouldBe(1);
        application.Labels!["group"].ShouldBe("commerce");
        repository.GetRecentObservations("orders", 10).ShouldHaveSingleItem();

        var history = repository.GetHistory(now.AddSeconds(1));
        history.StorageProvider.ShouldBe("InMemory");
        history.Durable.ShouldBeFalse();
        history.MetricRetentionSeconds.ShouldBe(900);
        history.LastIngestAtUtc.ShouldNotBeNull();
        history.OldestObservationAtUtc.ShouldBe(now);
        history.LatestObservationAtUtc.ShouldBe(now);
        history.Complete.ShouldBeTrue();
    }

    [Fact]
    public void Repository_derives_windowed_instance_metrics_retries_and_observed_flow()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("gateway", "gateway-1", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce"));

        repository.RecordBatch(CreateBatch(
            "gateway",
            "gateway-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-2), "sent", true, "SubmitOrder", "urn:message:SubmitOrder",
                null, "loopback://orders", 2, null, null, null, "conversation-1", null, null)));
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-1), "consumed", true, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 42, null, null, null, "conversation-1", null, null),
            new MonitoringObservation(
                2, now.AddMilliseconds(-500), "retry_attempted", false, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 0, typeof(InvalidOperationException).FullName, "retry", null, "conversation-2", null, null, 1, 2),
            new MonitoringObservation(
                3, now.AddMilliseconds(-250), "retry_exhausted", false, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 0, typeof(InvalidOperationException).FullName, "retry", null, "conversation-2", null, null, 3, 2)));

        var applicationRate = repository.GetRates("orders", 60, false, now).ShouldHaveSingleItem();
        applicationRate.InstanceId.ShouldBeNull();
        applicationRate.Counts.Consumed.ShouldBe(1);
        applicationRate.Counts.RetryAttempted.ShouldBe(1);
        applicationRate.Counts.RetryExhausted.ShouldBe(1);
        applicationRate.Rates.ConsumedPerSecond.ShouldBe(1d / 60d);
        applicationRate.AverageConsumeDurationMs.ShouldBe(42);
        applicationRate.P95ConsumeDurationMs.ShouldBe(50);

        var instanceRate = repository.GetRates("orders", 60, true, now).ShouldHaveSingleItem();
        instanceRate.InstanceId.ShouldBe("orders-1");

        var series = repository.GetTimeSeries("orders", 60, 5, false, now);
        series.Count.ShouldBe(13);
        series.Sum(point => point.Counts.Consumed).ShouldBe(1);
        series.ShouldAllBe(point => point.ApplicationName == "orders" && point.InstanceId == null);
        series.ShouldAllBe(point => point.Complete);

        var flow = repository.GetFlow(null, 60, now).ShouldHaveSingleItem();
        flow.SourceApplication.ShouldBe("gateway");
        flow.TargetApplication.ShouldBe("orders");
        flow.EndpointName.ShouldBe("orders");
        flow.Count.ShouldBe(1);
    }

    [Fact]
    public void Repository_rejects_unbounded_resource_labels()
    {
        var now = DateTimeOffset.UtcNow;
        var metadata = CreateMetadata("orders", "orders-1", now, "commerce") with
        {
            Labels = Enumerable.Range(0, 17).ToDictionary(index => $"label-{index}", _ => "value")
        };

        var repository = new MonitoringRepository();
        Should.Throw<MonitoringValidationException>(() => repository.UpsertMetadata(metadata));
    }

    [Fact]
    public void Repository_limits_incomplete_coverage_to_the_affected_window()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce"));
        var batch = CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now, "consumed", true, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 1, null, null, null, null, null, null)) with
        {
            DroppedObservations = 3
        };
        repository.RecordBatch(batch).ShouldBeTrue();

        var affected = repository.GetRates("orders", 60, false, now).ShouldHaveSingleItem();
        affected.Complete.ShouldBeFalse();
        affected.DroppedObservations.ShouldBe(3);

        var recovered = repository.GetRates("orders", 60, false, now.AddMinutes(2)).ShouldHaveSingleItem();
        recovered.Complete.ShouldBeTrue();
        recovered.DroppedObservations.ShouldBe(0);
    }

    [Fact]
    public void Repository_groups_replicas_by_application_and_keeps_only_common_labels()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("workers", "worker-1", now, "commerce") with
        {
            Labels = new Dictionary<string, string> { ["group"] = "commerce", ["zone"] = "north" }
        });
        repository.UpsertMetadata(CreateMetadata("workers", "worker-2", now, "commerce") with
        {
            Labels = new Dictionary<string, string> { ["group"] = "commerce", ["zone"] = "south" }
        });

        var application = repository.GetApplications(now).ShouldHaveSingleItem();
        application.ApplicationName.ShouldBe("workers");
        application.OnlineInstances.ShouldBe(2);
        application.TotalInstances.ShouldBe(2);
        application.Labels!.Count.ShouldBe(1);
        application.Labels["group"].ShouldBe("commerce");
        repository.GetInstances("workers", now).Count.ShouldBe(2);
    }

    [Fact]
    public void Repository_aggregates_flow_and_throughput_across_source_and_target_replicas()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("checkout", "checkout-1", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("checkout", "checkout-2", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("orders", "orders-2", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("orders", "orders-3", now, "commerce"));

        repository.RecordBatch(CreateBatch(
            "checkout",
            "checkout-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-4), "published", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                null, "exchange:orders", 2, null, null, null, "conversation-1", null, null))).ShouldBeTrue();
        repository.RecordBatch(CreateBatch(
            "checkout",
            "checkout-2",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-3), "published", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                null, "exchange:orders", 2, null, null, null, "conversation-2", null, null))).ShouldBeTrue();
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-2), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 10, null, null, null, "conversation-1", null, null))).ShouldBeTrue();
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-2",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-1), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 12, null, null, null, "conversation-2", null, null))).ShouldBeTrue();

        var flow = repository.GetFlow(null, 60, now).ShouldHaveSingleItem();
        flow.SourceApplication.ShouldBe("checkout");
        flow.TargetApplication.ShouldBe("orders");
        flow.Count.ShouldBe(2);

        var replicaFlow = repository.GetReplicaFlow(null, 60, now);
        replicaFlow.Count.ShouldBe(2);
        var firstReplicaPath = replicaFlow.Single(edge => edge.SourceInstanceId == "checkout-1");
        firstReplicaPath.SourceBusId.ShouldBe("bus");
        firstReplicaPath.TargetApplication.ShouldBe("orders");
        firstReplicaPath.TargetInstanceId.ShouldBe("orders-1");
        firstReplicaPath.TargetBusId.ShouldBe("bus");
        firstReplicaPath.Count.ShouldBe(1);
        var secondReplicaPath = replicaFlow.Single(edge => edge.SourceInstanceId == "checkout-2");
        secondReplicaPath.TargetInstanceId.ShouldBe("orders-2");
        repository.GetReplicaFlow("checkout", 60, now).Count.ShouldBe(2);
        repository.GetReplicaFlow("unrelated", 60, now).ShouldBeEmpty();

        var orderRate = repository.GetRates("orders", 60, false, now).ShouldHaveSingleItem();
        orderRate.InstanceId.ShouldBeNull();
        orderRate.Counts.Consumed.ShouldBe(2);
        repository.GetRates("orders", 60, true, now).Sum(rate => rate.Counts.Consumed).ShouldBe(2);
        repository.GetTimeSeries("orders", 60, 5, false, now)
            .Sum(point => point.Counts.Consumed)
            .ShouldBe(2);
    }

    [Fact]
    public void Repository_summarizes_outbox_dispatcher_state_and_windowed_throughput()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("orders-dispatcher", "replica-1", now, "commerce"));

        repository.RecordBatch(CreateBatch(
            "orders-dispatcher",
            "replica-1",
            now,
            CreateOutboxObservation(1, now.AddSeconds(-30), true, 9, 8, 1, 0, 12, 2, 1_500),
            CreateOutboxObservation(2, now.AddSeconds(-5), false, 7, 5, 2, 1, 9, 3, 2_500, "transport")));

        var dispatcher = repository.GetOutboxDispatchers("orders-dispatcher", 60, now).ShouldHaveSingleItem();
        dispatcher.ApplicationName.ShouldBe("orders-dispatcher");
        dispatcher.InstanceId.ShouldBe("replica-1");
        dispatcher.ServiceName.ShouldBe("orders");
        dispatcher.OwnerId.ShouldBe("dispatcher-a");
        dispatcher.Online.ShouldBeTrue();
        dispatcher.LastCycleSucceeded.ShouldBeFalse();
        dispatcher.LastFailureCategory.ShouldBe("transport");
        dispatcher.Pending.ShouldBe(9);
        dispatcher.Retrying.ShouldBe(3);
        dispatcher.OldestUndispatchedAgeMs.ShouldBe(2_500);
        dispatcher.WindowLeased.ShouldBe(16);
        dispatcher.WindowDispatched.ShouldBe(13);
        dispatcher.WindowFailed.ShouldBe(3);
        dispatcher.WindowLostLeases.ShouldBe(1);
        dispatcher.DispatchedPerSecond.ShouldBe(13d / 60d);
    }

    [Fact]
    public void Repository_combines_endpoint_topology_across_replicas_with_recent_activity()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        var endpoint = new ReceiveEndpointInspection(
            "orders",
            "rabbitmq://localhost/orders",
            [new MessageBindingInspection("SubmitOrder", "urn:message:SubmitOrder", "submit-order", new Dictionary<string, object?>())],
            ["SubmitOrderConsumer"],
            new TransportInspectionDetails("rabbitmq", new Dictionary<string, object?>()),
            new Dictionary<string, object?>());
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce") with
        {
            Bus = new BusInspectionSnapshot("rabbitmq", new Uri("rabbitmq://localhost/"), now, [], [endpoint], [])
        });
        repository.UpsertMetadata(CreateMetadata("orders", "orders-2", now.AddMinutes(-1), "commerce") with
        {
            Bus = new BusInspectionSnapshot("rabbitmq", new Uri("rabbitmq://localhost/"), now, [], [endpoint], [])
        });
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-5), "consumed", true, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 12, null, null, null, null, null, null),
            new MonitoringObservation(
                2, now.AddSeconds(-4), "retry_attempted", false, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 12, "TimeoutException", "retry", null, null, null, null, 1, 3),
            new MonitoringObservation(
                3, now.AddSeconds(-3), "consume_faulted", false, "SubmitOrder", "urn:message:SubmitOrder",
                "orders", null, 12, "TimeoutException", "failed", null, null, null, null)));

        var summary = repository.GetEndpoints(null, 60, now).ShouldHaveSingleItem();
        summary.ApplicationName.ShouldBe("orders");
        summary.EndpointName.ShouldBe("orders");
        summary.TransportName.ShouldBe("rabbitmq");
        summary.OnlineInstances.ShouldBe(1);
        summary.TotalInstances.ShouldBe(2);
        summary.ConsumerCount.ShouldBe(1);
        summary.MessageTypeCount.ShouldBe(1);
        summary.Consumed.ShouldBe(1);
        summary.Retried.ShouldBe(1);
        summary.Faulted.ShouldBe(1);
        summary.ConsumedPerSecond.ShouldBe(1d / 60d);
        summary.LastActivityAtUtc.ShouldBe(now.AddSeconds(-3));
    }

    private static MonitoringMetadata CreateMetadata(
        string applicationName,
        string instanceId,
        DateTimeOffset now,
        string group)
        => new(
            MonitoringProtocol.Version,
            applicationName,
            instanceId,
            "1.0.0",
            "dotnet",
            "1.0.0",
            "bus",
            now,
            now,
            new BusInspectionSnapshot("mediator", new Uri("loopback://localhost/"), now, [], [], []),
            new Dictionary<string, string> { ["group"] = group });

    private static MonitoringObservationBatch CreateBatch(
        string applicationName,
        string instanceId,
        DateTimeOffset now,
        params MonitoringObservation[] observations)
        => new(
            MonitoringProtocol.Version,
            applicationName,
            instanceId,
            "bus",
            Guid.NewGuid().ToString("N"),
            observations[0].Sequence,
            observations[^1].Sequence,
            0,
            now,
            observations);

    private static MonitoringObservation CreateOutboxObservation(
        long sequence,
        DateTimeOffset occurredAtUtc,
        bool succeeded,
        int batchLeased,
        int batchDispatched,
        int batchFailed,
        int batchLostLeases,
        int pending,
        int retrying,
        double oldestUndispatchedAgeMs,
        string? failureCategory = null)
        => new(
            sequence,
            occurredAtUtc,
            "outbox_dispatch_cycle",
            succeeded,
            null,
            null,
            "orders",
            null,
            12.5,
            failureCategory,
            null,
            null,
            null,
            null,
            null,
            Properties: new Dictionary<string, string>
            {
                ["service_name"] = "orders",
                ["owner_id"] = "dispatcher-a",
                ["batch_leased"] = batchLeased.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["batch_dispatched"] = batchDispatched.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["batch_failed"] = batchFailed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["batch_lost_leases"] = batchLostLeases.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["pending"] = pending.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["leased"] = "2",
                ["retrying"] = retrying.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["stored_dispatched"] = "40",
                ["dead"] = "1",
                ["cancelled"] = "4",
                ["oldest_undispatched_age_ms"] = oldestUndispatchedAgeMs.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

    private sealed record TestMessage(string Value);
}
