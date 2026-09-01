using MyServiceBus.Inspection;
using MyServiceBus;
using MyServiceBus.Choreography;
using MyServiceBus.Monitoring;
using MyServiceBus.Monitoring.Server;
using MyServiceBus.Orchestration;
using MyServiceBus.Topology;
using Shouldly;

public class MonitoringRepositoryTests
{
    [Fact]
    public void Repository_keeps_saga_definitions_separate_and_reports_deployment_conflicts()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        var version1 = CreateSagaDefinition("1");
        var version2 = CreateSagaDefinition("2");
        repository.UpsertMetadata(WithSaga(
            CreateMetadata("orders", "orders-1", now, "commerce"),
            version1,
            "order-saga"));
        repository.UpsertMetadata(WithSaga(
            CreateMetadata("orders", "orders-2", now, "commerce"),
            version1,
            "order-saga"));
        repository.UpsertMetadata(WithSaga(
            CreateMetadata("orders-v2", "orders-v2-1", now, "commerce"),
            version2,
            "order-saga-v2"));

        var saga = repository.GetDeclaredSagaStateMachines(now).ShouldHaveSingleItem();

        saga.StateMachineId.ShouldBe("order-state-machine");
        saga.DefinitionVersions.ShouldBe(["1", "2"]);
        saga.ConflictKinds.ShouldContain("definition_version_conflict");
        saga.Deployments.Count.ShouldBe(2);
        saga.Deployments.Single(item => item.Definition.DefinitionVersion == "1").InstanceCount.ShouldBe(2);
        repository.GetDeclaredChoreographies(now).ShouldBeEmpty();
    }

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
    public void Repository_merges_declared_choreography_replicas_and_reports_definition_conflicts()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        var orders = CreateChoreography(
            "orders", "1", "accept-order", "urn:message:OrderSubmitted", "urn:message:OrderAccepted");
        var inventory = CreateChoreography(
            "inventory", "1", "reserve-inventory", "urn:message:OrderAccepted", null);
        var billing = CreateChoreography(
            "billing", "1", "reserve-inventory", "urn:message:OrderAccepted", null);
        var legacyOrders = CreateChoreography(
            "orders", "2", "accept-legacy-order", "urn:message:OrderAccepted", null);

        repository.UpsertMetadata(WithChoreography(CreateMetadata("orders", "orders-1", now, "commerce"), orders));
        repository.UpsertMetadata(WithChoreography(CreateMetadata("orders", "orders-2", now.AddMinutes(-1), "commerce"), orders));
        repository.UpsertMetadata(WithChoreography(CreateMetadata("inventory", "inventory-1", now, "commerce"), inventory));
        repository.UpsertMetadata(WithChoreography(CreateMetadata("billing", "billing-1", now, "commerce"), billing));
        repository.UpsertMetadata(WithChoreography(CreateMetadata("legacy-orders", "legacy-orders-1", now, "commerce"), legacyOrders));

        var choreography = repository.GetDeclaredChoreographies(now).ShouldHaveSingleItem();
        choreography.ChoreographyId.ShouldBe("order-fulfillment");
        choreography.DefinitionVersions.ShouldBe(["1", "2"]);
        choreography.ConflictKinds.ShouldBe(["definition_version", "owner", "step_ownership"]);
        choreography.LastCapturedAtUtc.ShouldBe(now);
        choreography.Fragments.Count.ShouldBe(4);
        choreography.Connections.Count.ShouldBe(2);
        choreography.Connections.Select(connection => connection.TargetApplication)
            .ShouldBe(["billing", "inventory"]);
        choreography.Connections.ShouldAllBe(connection =>
            connection.DefinitionVersion == "1" && connection.MatchKind == "declared_contract");

        var orderFragment = choreography.Fragments.Single(fragment => fragment.ApplicationName == "orders");
        orderFragment.Owner.ShouldBe("orders");
        orderFragment.ReportingInstances.ShouldBe(2);
        orderFragment.OnlineInstances.ShouldBe(1);
        orderFragment.Steps.ShouldHaveSingleItem().Id.ShouldBe("accept-order");
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
        flow.MatchConfidence.ShouldBe("correlated");

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
    public void Repository_prefers_exact_message_identity_when_correlation_is_shared()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(CreateMetadata("checkout", "checkout-1", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("checkout", "checkout-2", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("orders", "orders-1", now, "commerce"));
        repository.UpsertMetadata(CreateMetadata("orders", "orders-2", now, "commerce"));

        repository.RecordBatch(CreateBatch(
            "checkout",
            "checkout-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-4), "published", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                null, "exchange:orders", 2, null, null, "shared", "conversation", null, null,
                MessageId: "message-1"))).ShouldBeTrue();
        repository.RecordBatch(CreateBatch(
            "checkout",
            "checkout-2",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-3), "published", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                null, "exchange:orders", 2, null, null, "shared", "conversation", null, null,
                MessageId: "message-2"))).ShouldBeTrue();
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-2), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 10, null, null, "shared", "conversation", null, null,
                MessageId: "message-1"))).ShouldBeTrue();
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-2",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-1), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 12, null, null, "shared", "conversation", null, null,
                MessageId: "message-2"))).ShouldBeTrue();

        var replicaFlow = repository.GetReplicaFlow(null, 60, now);
        replicaFlow.Count.ShouldBe(2);
        replicaFlow.ShouldAllBe(edge => edge.MatchConfidence == "exact_message");
        replicaFlow.Single(edge => edge.SourceInstanceId == "checkout-1").TargetInstanceId.ShouldBe("orders-1");
        replicaFlow.Single(edge => edge.SourceInstanceId == "checkout-2").TargetInstanceId.ShouldBe("orders-2");

        var flow = repository.GetFlow(null, 60, now).ShouldHaveSingleItem();
        flow.Count.ShouldBe(2);
        flow.MatchConfidence.ShouldBe("exact_message");
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
    public void Repository_projects_exact_consume_to_outbound_causal_reactions()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        var choreography = new ChoreographyBuilder("order-fulfillment", "1", "orders")
            .Step("request-inventory", "urn:message:OrderSubmitted", step => step
                .Publishes("urn:message:InventoryRequested"))
            .Build();
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now, "commerce"), choreography));
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-2), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 5, null, null, null, "conversation", null, null,
                MessageId: "incoming-1"),
            new MonitoringObservation(
                2, now.AddSeconds(-1), "published", true, "InventoryRequested", "urn:message:InventoryRequested",
                null, "exchange:inventory", 2, null, null, null, "conversation", null, null,
                MessageId: "outgoing-1", CausationMessageId: "incoming-1")));

        var edge = repository.GetCausalFlow(null, 60, now).ShouldHaveSingleItem();
        edge.ApplicationName.ShouldBe("orders");
        edge.ConsumerEndpointName.ShouldBe("orders");
        edge.TriggerMessageUrn.ShouldBe("urn:message:OrderSubmitted");
        edge.OutputMessageUrn.ShouldBe("urn:message:InventoryRequested");
        edge.OperationKind.ShouldBe("published");
        edge.Count.ShouldBe(1);
        edge.MatchConfidence.ShouldBe("exact_causation");

        var runtime = repository.GetChoreographyRuntime(60, now);
        runtime.WindowSeconds.ShouldBe(60);
        runtime.Complete.ShouldBeTrue();
        var reaction = runtime.Reactions.ShouldHaveSingleItem();
        reaction.ChoreographyId.ShouldBe("order-fulfillment");
        reaction.StepId.ShouldBe("request-inventory");
        reaction.ObservedCount.ShouldBe(1);
        reaction.EvidenceStatus.ShouldBe("exact_causation");

        var run = repository.GetChoreographyRuns("order-fulfillment", 60, 20, now).Runs.ShouldHaveSingleItem();
        run.CoordinationType.ShouldBe("choreography");
        run.LifecycleAuthority.ShouldBe("reconstructed_evidence");
        run.EvidenceComplete.ShouldBeTrue();
        run.Status.ShouldBe("live");
        run.LastActivityAtUtc.ShouldBe(now.AddSeconds(-1));
        var runStep = run.Steps.ShouldHaveSingleItem();
        runStep.StepId.ShouldBe("request-inventory");
        var expectation = runStep.OutputExpectations.ShouldHaveSingleItem();
        expectation.Status.ShouldBe("exact_observed");
        expectation.ObservedCount.ShouldBe(1);
        run.DiagnosticIssueCount.ShouldBe(0);
        run.IndeterminateExpectationCount.ShouldBe(0);

        repository.CaptureWorkflowRuns(now);
        var retained = repository.GetWorkflowRuns(
            "order-fulfillment", "choreography", "live", "incoming-1", null, null, 0, 10, now);
        retained.Total.ShouldBe(1);
        retained.Runs.ShouldHaveSingleItem().RunId.ShouldBe(run.RunId);
        repository.GetWorkflowRun(run.RunId, now)!.RunId.ShouldBe(run.RunId);
        repository.GetWorkflowRun(run.RunId, now.AddSeconds(30))!.Status.ShouldBe("no_recent_activity");
    }

    [Fact]
    public void Repository_compares_declared_outputs_with_complete_exact_run_evidence()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        var choreography = new ChoreographyBuilder("order-diagnostics", "1", "orders")
            .Step("check-order", "urn:message:OrderSubmitted", step => step
                .Publishes("urn:message:InventoryRequested", output => output.AtLeast(2).Within(TimeSpan.FromSeconds(2)))
                .Publishes("urn:message:PaymentRequested", output => output.Within(TimeSpan.FromMilliseconds(100)))
                .Publishes("urn:message:NoOutputRequired", output => output.Exactly(0)))
            .Build();
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now, "commerce"), choreography));
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-30), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 5, null, null, null, "conversation", null, null,
                MessageId: "root-message"),
            new MonitoringObservation(
                2, now.AddMilliseconds(-29_500), "published", true, "InventoryRequested", "urn:message:InventoryRequested",
                null, "exchange:inventory", 2, null, null, null, "conversation", null, null,
                MessageId: "inventory-message", CausationMessageId: "root-message"),
            new MonitoringObservation(
                3, now.AddMilliseconds(-29_400), "published", true, "PaymentRequested", "urn:message:PaymentRequested",
                null, "exchange:payment", 2, null, null, null, "conversation", null, null,
                MessageId: "payment-message", CausationMessageId: "root-message"),
            new MonitoringObservation(
                4, now.AddMilliseconds(-29_300), "published", true, "AuditRecorded", "urn:message:AuditRecorded",
                null, "exchange:audit", 2, null, null, null, "conversation", null, null,
                MessageId: "audit-message", CausationMessageId: "root-message")));

        var run = repository.GetChoreographyRuns("order-diagnostics", 60, 20, now).Runs.ShouldHaveSingleItem();
        var expectations = run.Steps.ShouldHaveSingleItem().OutputExpectations;
        var inventory = expectations.Single(value => value.MessageUrn == "urn:message:InventoryRequested");
        inventory.Status.ShouldBe("below_minimum");
        inventory.MinimumCount.ShouldBe(2);
        inventory.ObservedCount.ShouldBe(1);
        var payment = expectations.Single(value => value.MessageUrn == "urn:message:PaymentRequested");
        payment.Status.ShouldBe("timing_exceeded");
        payment.LateCount.ShouldBe(1);
        var unexpected = expectations.Single(value => value.MessageUrn == "urn:message:AuditRecorded");
        unexpected.Status.ShouldBe("unexpected_observed");
        unexpected.Requirement.ShouldBe("undeclared");
        expectations.Single(value => value.MessageUrn == "urn:message:NoOutputRequired")
            .Status.ShouldBe("expectation_satisfied");
        run.DiagnosticIssueCount.ShouldBe(3);
        run.IndeterminateExpectationCount.ShouldBe(0);
    }

    [Fact]
    public void Repository_does_not_claim_missing_outputs_when_run_evidence_is_incomplete()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        var choreography = new ChoreographyBuilder("order-diagnostics", "1", "orders")
            .Step("check-order", "urn:message:OrderSubmitted", step => step
                .Publishes("urn:message:InventoryRequested"))
            .Build();
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now.AddMinutes(-1), "commerce"), choreography));
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-30), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 5, null, null, null, "conversation", null, null,
                MessageId: "root-message")) with
        { DroppedObservations = 1 });

        var run = repository.GetChoreographyRuns("order-diagnostics", 60, 20, now).Runs.ShouldHaveSingleItem();
        run.EvidenceComplete.ShouldBeFalse();
        run.Steps.ShouldHaveSingleItem().OutputExpectations.ShouldHaveSingleItem()
            .Status.ShouldBe("insufficient_evidence");
        run.DiagnosticIssueCount.ShouldBe(0);
        run.IndeterminateExpectationCount.ShouldBe(1);

        var liveRepository = new MonitoringRepository();
        liveRepository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now, "commerce"), choreography));
        liveRepository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-1), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 5, null, null, null, "conversation", null, null,
                MessageId: "live-root")));
        var liveRun = liveRepository.GetChoreographyRuns("order-diagnostics", 60, 20, now).Runs.ShouldHaveSingleItem();
        liveRun.Steps.ShouldHaveSingleItem().OutputExpectations.ShouldHaveSingleItem()
            .Status.ShouldBe("awaiting_evidence");
        liveRepository.CaptureWorkflowRuns(now);
        liveRepository.CaptureWorkflowRuns(now.AddSeconds(30));
        liveRepository.GetWorkflowRun(liveRun.RunId, now.AddSeconds(30))!
            .Steps.ShouldHaveSingleItem().OutputExpectations.ShouldHaveSingleItem()
            .Status.ShouldBe("missing_expected");
    }

    [Fact]
    public void Repository_reconstructs_exact_declared_choreography_runs_with_step_timing_and_failures()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        var orders = new ChoreographyBuilder("order-fulfillment", "1", "orders")
            .Step("request-inventory", "urn:message:OrderSubmitted", step => step
                .OwnedBy("OrdersConsumer")
                .Publishes("urn:message:InventoryRequested"))
            .Build();
        var inventory = new ChoreographyBuilder("order-fulfillment", "1", "inventory")
            .Step("reserve-inventory", "urn:message:InventoryRequested", step => step
                .OwnedBy("InventoryConsumer")
                .Terminates())
            .Build();
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now, "commerce"), orders));
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("inventory", "inventory-1", now, "commerce"), inventory));
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddMilliseconds(-8_150), "retry_attempted", false,
                "OrderSubmitted", "urn:message:OrderSubmitted", null, null, 0,
                typeof(InvalidOperationException).FullName, null, null, "conversation", null, null,
                RetryAttempt: 1, RetryLimit: 3, MessageId: "root-message"),
            new MonitoringObservation(
                2, now.AddMilliseconds(-8_100), "published", true,
                "InventoryRequested", "urn:message:InventoryRequested", null, "exchange:inventory", 10,
                null, null, null, "conversation", null, null,
                MessageId: "inventory-message", CausationMessageId: "root-message"),
            new MonitoringObservation(
                3, now.AddMilliseconds(-8_000), "consumed", true,
                "OrderSubmitted", "urn:message:OrderSubmitted", "orders", null, 200,
                null, null, null, "conversation", null, null,
                MessageId: "root-message")));
        repository.RecordBatch(CreateBatch(
            "inventory",
            "inventory-1",
            now,
            new MonitoringObservation(
                1, now.AddMilliseconds(-7_000), "consume_faulted", false,
                "InventoryRequested", "urn:message:InventoryRequested", "inventory", null, 300,
                typeof(InvalidOperationException).FullName, null, null, "conversation", null, null,
                MessageId: "inventory-message")));

        var snapshot = repository.GetChoreographyRuns("order-fulfillment", 60, 20, now);

        snapshot.Complete.ShouldBeTrue();
        var run = snapshot.Runs.ShouldHaveSingleItem();
        run.RootMessageId.ShouldBe("root-message");
        run.Status.ShouldBe("faulted");
        run.Confidence.ShouldBe("exact_causation");
        run.Steps.Count.ShouldBe(2);
        var first = run.Steps[0];
        first.StepId.ShouldBe("request-inventory");
        first.OwnerComponent.ShouldBe("OrdersConsumer");
        first.DurationMs.ShouldBe(200);
        first.RetryCount.ShouldBe(1);
        var output = first.Outputs.ShouldHaveSingleItem();
        output.OperationKind.ShouldBe("published");
        output.Targets.ShouldHaveSingleItem().HandoffDurationMs.ShouldBe(810);
        var second = run.Steps[1];
        second.StepId.ShouldBe("reserve-inventory");
        second.Status.ShouldBe("faulted");
        second.DurationMs.ShouldBe(300);
        second.FailureType.ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void Repository_keeps_exact_fan_out_in_one_run_and_reports_branch_structure()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now, "commerce"),
            CreateChoreography("orders", "1", "dispatch-order", "urn:message:OrderSubmitted", "urn:message:OrderAccepted")));
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("inventory", "inventory-1", now, "commerce"),
            CreateChoreography("inventory", "1", "reserve-inventory", "urn:message:OrderAccepted", null)));
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("billing", "billing-1", now, "commerce"),
            CreateChoreography("billing", "1", "authorize-payment", "urn:message:OrderAccepted", null)));

        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-4), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 10, null, null, null, "conversation", null, null,
                MessageId: "root-message"),
            new MonitoringObservation(
                2, now.AddSeconds(-3), "published", true, "OrderAccepted", "urn:message:OrderAccepted",
                null, "exchange:accepted", 2, null, null, null, "conversation", null, null,
                MessageId: "fan-out-message", CausationMessageId: "root-message")));
        repository.RecordBatch(CreateBatch(
            "inventory",
            "inventory-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-2), "consumed", true, "OrderAccepted", "urn:message:OrderAccepted",
                "inventory", null, 8, null, null, null, "conversation", null, null,
                MessageId: "fan-out-message")));
        repository.RecordBatch(CreateBatch(
            "billing",
            "billing-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-1), "consumed", true, "OrderAccepted", "urn:message:OrderAccepted",
                "billing", null, 7, null, null, null, "conversation", null, null,
                MessageId: "fan-out-message")));

        var run = repository.GetChoreographyRuns("order-fulfillment", 60, 20, now).Runs.ShouldHaveSingleItem();
        run.Steps.Count.ShouldBe(3);
        run.RootMessageIds.ShouldBe(["root-message"]);
        run.RootCount.ShouldBe(1);
        run.BranchPointCount.ShouldBe(1);
        run.MergePointCount.ShouldBe(0);
        run.ObservedShape.ShouldBe("branching");
        run.Steps.Single(step => step.StepId == "dispatch-order")
            .Outputs.ShouldHaveSingleItem().Targets.Count.ShouldBe(2);
    }

    [Fact]
    public void Repository_groups_root_message_delivery_fan_out_under_one_run_identity()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now, "commerce"),
            CreateChoreography("orders", "1", "observe-order", "urn:message:OrderSubmitted", null)));
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("analytics", "analytics-1", now, "commerce"),
            CreateChoreography("analytics", "1", "measure-order", "urn:message:OrderSubmitted", null)));
        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-2), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 10, null, null, null, "conversation", null, null,
                MessageId: "shared-root")));
        repository.RecordBatch(CreateBatch(
            "analytics",
            "analytics-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-1), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "analytics", null, 8, null, null, null, "conversation", null, null,
                MessageId: "shared-root")));

        var run = repository.GetChoreographyRuns("order-fulfillment", 60, 20, now).Runs.ShouldHaveSingleItem();
        run.Steps.Count.ShouldBe(2);
        run.RootMessageIds.ShouldBe(["shared-root"]);
        run.RootCount.ShouldBe(1);
        run.BranchPointCount.ShouldBe(1);
        run.MergePointCount.ShouldBe(0);
        run.ObservedShape.ShouldBe("branching");
    }

    [Fact]
    public void Repository_merges_exactly_connected_roots_instead_of_claiming_a_shared_descendant()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new MonitoringRepository();
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("orders", "orders-1", now, "commerce"),
            CreateChoreography("orders", "1", "accept-order", "urn:message:OrderSubmitted", "urn:message:Ready")));
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("billing", "billing-1", now, "commerce"),
            CreateChoreography("billing", "1", "accept-payment", "urn:message:PaymentSubmitted", "urn:message:Ready")));
        repository.UpsertMetadata(WithChoreography(
            CreateMetadata("fulfillment", "fulfillment-1", now, "commerce"),
            CreateChoreography("fulfillment", "1", "complete-order", "urn:message:Ready", null)));

        repository.RecordBatch(CreateBatch(
            "orders",
            "orders-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-6), "consumed", true, "OrderSubmitted", "urn:message:OrderSubmitted",
                "orders", null, 10, null, null, null, "conversation", null, null,
                MessageId: "root-order"),
            new MonitoringObservation(
                2, now.AddSeconds(-4), "published", true, "Ready", "urn:message:Ready",
                null, "exchange:ready", 2, null, null, null, "conversation", null, null,
                MessageId: "shared-message", CausationMessageId: "root-order")));
        repository.RecordBatch(CreateBatch(
            "billing",
            "billing-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-5), "consumed", true, "PaymentSubmitted", "urn:message:PaymentSubmitted",
                "billing", null, 9, null, null, null, "conversation", null, null,
                MessageId: "root-payment"),
            new MonitoringObservation(
                2, now.AddSeconds(-3), "published", true, "Ready", "urn:message:Ready",
                null, "exchange:ready", 2, null, null, null, "conversation", null, null,
                MessageId: "shared-message", CausationMessageId: "root-payment")));

        repository.CaptureWorkflowRuns(now.AddSeconds(-2.5));
        repository.GetWorkflowRuns(
            "order-fulfillment", null, null, null, null, null, 0, 10, now.AddSeconds(-2.5)).Total.ShouldBe(2);

        repository.RecordBatch(CreateBatch(
            "fulfillment",
            "fulfillment-1",
            now,
            new MonitoringObservation(
                1, now.AddSeconds(-2), "consumed", true, "Ready", "urn:message:Ready",
                "fulfillment", null, 8, null, null, null, "conversation", null, null,
                MessageId: "shared-message")));

        var run = repository.GetChoreographyRuns("order-fulfillment", 60, 20, now).Runs.ShouldHaveSingleItem();
        run.Steps.Count.ShouldBe(3);
        run.RootMessageIds.ShouldBe(["root-order", "root-payment"]);
        run.RootCount.ShouldBe(2);
        run.BranchPointCount.ShouldBe(0);
        run.MergePointCount.ShouldBe(1);
        run.ObservedShape.ShouldBe("converging");

        repository.CaptureWorkflowRuns(now);
        var retained = repository.GetWorkflowRuns(
            "order-fulfillment", null, null, null, null, null, 0, 10, now);
        retained.Total.ShouldBe(1);
        retained.Runs.ShouldHaveSingleItem().RootMessageIds.ShouldBe(["root-order", "root-payment"]);
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

    private static MonitoringMetadata WithChoreography(
        MonitoringMetadata metadata,
        ChoreographyFragment fragment)
        => metadata with
        {
            Bus = new BusInspectionSnapshot(
                metadata.Bus.TransportName,
                metadata.Bus.Address,
                metadata.Bus.CapturedAt,
                metadata.Bus.Messages,
                metadata.Bus.ReceiveEndpoints,
                metadata.Bus.Consumers,
                [fragment])
        };

    private static MonitoringMetadata WithSaga(
        MonitoringMetadata metadata,
        SagaStateMachineDefinition definition,
        string endpointName)
        => metadata with
        {
            Bus = new BusInspectionSnapshot(
                metadata.Bus.TransportName,
                metadata.Bus.Address,
                metadata.Bus.CapturedAt,
                metadata.Bus.Messages,
                metadata.Bus.ReceiveEndpoints,
                metadata.Bus.Consumers,
                sagaStateMachines: [new SagaStateMachineTopology(definition, endpointName)])
        };

    private static SagaStateMachineDefinition CreateSagaDefinition(string definitionVersion)
        => new SagaStateMachineDefinitionBuilder(
                "order-state-machine",
                definitionVersion,
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
            .State("Running")
            .Event("OrderSubmitted", "urn:message:Contracts:OrderSubmitted", @event => @event
                .CorrelateById("CorrelationId", "OrderId")
                .CreatesIfMissing())
            .Initially("OrderSubmitted", behavior => behavior.TransitionTo("Running"))
            .Build();

    private static ChoreographyFragment CreateChoreography(
        string owner,
        string definitionVersion,
        string stepId,
        string triggerMessageUrn,
        string? outputMessageUrn)
        => new ChoreographyBuilder("order-fulfillment", definitionVersion, owner)
            .Step(stepId, triggerMessageUrn, step =>
            {
                if (outputMessageUrn is null)
                    step.Terminates();
                else
                    step.Publishes(outputMessageUrn);
            })
            .Build();

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
