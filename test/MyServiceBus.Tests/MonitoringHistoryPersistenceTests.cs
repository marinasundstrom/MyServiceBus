using Microsoft.EntityFrameworkCore;
using MyServiceBus.Inspection;
using MyServiceBus.Monitoring;
using MyServiceBus.Monitoring.Server;
using Shouldly;

public class MonitoringHistoryPersistenceTests
{
    [Fact]
    public void Entity_framework_model_maps_the_dedicated_monitoring_schema()
    {
        var options = new DbContextOptionsBuilder<MonitoringHistoryDbContext>()
            .UseNpgsql("Host=localhost;Database=monitoring;Username=test;Password=test")
            .Options;
        using var context = new MonitoringHistoryDbContext(options);

        var script = context.Database.GenerateCreateScript();

        script.ShouldContain("myservicebus_monitoring");
        script.ShouldContain("observation_batch");
        script.ShouldContain("job_snapshot");
        script.ShouldContain("workflow_run");
        script.ShouldContain("saga_instance");
        script.ShouldContain("jsonb");
        context.Database.GetMigrations().ShouldContain("20260831120000_InitialMonitoringHistory");
        context.Database.GetMigrations().ShouldContain("20260831170000_AddRecurringJobSnapshots");
        context.Database.GetMigrations().ShouldContain("20260901123000_AddWorkflowRuns");
        context.Database.GetMigrations().ShouldContain("20260901194500_AddSagaInstances");
        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public async Task Startup_restore_rebuilds_the_live_query_model_and_durable_history_status()
    {
        var now = DateTimeOffset.UtcNow;
        var metadata = CreateMetadata(now);
        var batch = CreateBatch(now);
        var jobs = CreateJobs(now);
        var heartbeat = new MonitoringHeartbeat(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "bus",
            now);
        var store = new StubHistoryStore(new MonitoringHistoryRestore(
            [metadata],
            [batch],
            [heartbeat],
            [],
            [],
            [jobs],
            [],
            [CreateSagaInstance(now)],
            now));
        var repository = new MonitoringRepository();
        var restore = new MonitoringHistoryRestoreService(store, repository);

        await restore.StartAsync(CancellationToken.None);

        repository.GetApplications(now).ShouldHaveSingleItem().Totals.Consumed.ShouldBe(1);
        repository.GetJobs("orders", "running", now).ShouldHaveSingleItem()
            .Job.JobType.ShouldBe("invoice-export");
        repository.GetSagaInstance("order-state-machine", "saga-1").ShouldNotBeNull()
            .CurrentState.ShouldBe("AwaitingPayment");
        var history = new MonitoringIngestService(repository, store).GetHistory(now);
        history.StorageProvider.ShouldBe("PostgreSql");
        history.Durable.ShouldBeTrue();
        history.LastIngestAtUtc.ShouldBe(now);
        store.Initialized.ShouldBeTrue();
    }

    [Fact]
    public async Task Ingest_service_writes_accepted_monitoring_records_to_the_configured_store()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new StubHistoryStore(new MonitoringHistoryRestore([], [], [], [], [], [], [], [], null));
        var service = new MonitoringIngestService(new MonitoringRepository(), store);
        var metadata = CreateMetadata(now);
        var batch = CreateBatch(now);
        var sagaBatch = CreateSagaBatch(now);
        var jobs = CreateJobs(now);

        await service.UpsertMetadataAsync(metadata, CancellationToken.None);
        (await service.RecordBatchAsync(batch, CancellationToken.None)).ShouldBeTrue();
        (await service.RecordBatchAsync(sagaBatch, CancellationToken.None)).ShouldBeTrue();
        (await service.StoreJobsAsync(jobs, CancellationToken.None)).ShouldBeTrue();

        store.StoredMetadata.ShouldBe(1);
        store.StoredBatches.ShouldBe(2);
        store.StoredJobs.ShouldBe(1);
        store.StoredSagaInstances.ShouldBe(1);
    }

    private static MonitoringMetadata CreateMetadata(DateTimeOffset now)
        => new(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "1.0.0",
            "dotnet",
            "1.0.0",
            "bus",
            now,
            now,
            new BusInspectionSnapshot("rabbitmq", new Uri("rabbitmq://localhost/"), now, [], [], []));

    private static MonitoringObservationBatch CreateBatch(DateTimeOffset now)
        => new(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "bus",
            "batch-1",
            1,
            1,
            0,
            now,
            [new MonitoringObservation(
                1,
                now,
                "consumed",
                true,
                "SubmitOrder",
                "urn:message:SubmitOrder",
                "orders",
                null,
                12,
                null,
                null,
                null,
                null,
                null,
                null)]);

    private static MonitoringObservationBatch CreateSagaBatch(DateTimeOffset now)
        => new(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "bus",
            "batch-saga",
            2,
            2,
            0,
            now,
            [new MonitoringObservation(
                2,
                now,
                "saga_delivery",
                true,
                null,
                null,
                null,
                null,
                5,
                null,
                null,
                "saga-1",
                null,
                null,
                null,
                Properties: new Dictionary<string, string>
                {
                    ["state_machine_id"] = "order-state-machine",
                    ["definition_version"] = "1",
                    ["event_id"] = "OrderSubmitted",
                    ["status"] = "consumed",
                    ["begin_state"] = "Initial",
                    ["end_state"] = "AwaitingPayment",
                    ["created"] = "true",
                    ["completed"] = "false",
                    ["instance_present"] = "true"
                },
                MessageId: "message-1")]);

    private static MonitoringSagaInstance CreateSagaInstance(DateTimeOffset now)
        => new(
            "order-state-machine",
            "1",
            "orders",
            "saga-1",
            "active",
            "AwaitingPayment",
            true,
            true,
            now,
            now,
            null,
            [new MonitoringSagaTransition(
                now,
                "OrderSubmitted",
                "consumed",
                "Initial",
                "AwaitingPayment",
                true,
                true,
                false,
                true,
                5,
                null,
                null,
                "message-1")]);

    private static MonitoringJobSnapshot CreateJobs(DateTimeOffset now)
        => new(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "bus",
            now,
            [new MonitoringJobItem(
                "job-1",
                "invoice-export",
                "Running",
                "MyServiceBus.InMemory",
                "Volatile",
                "ProcessLocal",
                now.AddMinutes(-1),
                null,
                now.AddSeconds(-5),
                null,
                4,
                10,
                null,
                now,
                [])]);

    private sealed class StubHistoryStore : IMonitoringHistoryStore
    {
        private readonly MonitoringHistoryRestore restore;

        public StubHistoryStore(MonitoringHistoryRestore restore)
        {
            this.restore = restore;
        }

        public string Provider => "PostgreSql";
        public bool Durable => true;
        public DateTimeOffset? HistoryAvailableFromUtc => restore.LastIngestAtUtc;
        public bool Initialized { get; private set; }
        public int StoredMetadata { get; private set; }
        public int StoredBatches { get; private set; }
        public int StoredJobs { get; private set; }
        public int StoredWorkflowRuns { get; private set; }
        public int StoredSagaInstances { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task<MonitoringHistoryRestore> RestoreAsync(DateTimeOffset observationCutoff, CancellationToken cancellationToken)
            => Task.FromResult(restore);

        public Task StoreMetadataAsync(MonitoringMetadata metadata, CancellationToken cancellationToken)
        {
            StoredMetadata++;
            return Task.CompletedTask;
        }

        public Task StoreBatchAsync(MonitoringObservationBatch batch, CancellationToken cancellationToken)
        {
            StoredBatches++;
            return Task.CompletedTask;
        }

        public Task StoreHeartbeatAsync(MonitoringHeartbeat heartbeat, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreScheduledWorkAsync(MonitoringScheduledWorkSnapshot snapshot, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreRecurringJobsAsync(MonitoringRecurringJobSnapshot snapshot, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreJobsAsync(MonitoringJobSnapshot snapshot, CancellationToken cancellationToken)
        {
            StoredJobs++;
            return Task.CompletedTask;
        }

        public Task StoreWorkflowRunsAsync(
            IReadOnlyList<MonitoringChoreographyRun> runs,
            CancellationToken cancellationToken)
        {
            StoredWorkflowRuns += runs.Count;
            return Task.CompletedTask;
        }

        public Task StoreSagaInstancesAsync(
            IReadOnlyList<MonitoringSagaInstance> instances,
            CancellationToken cancellationToken)
        {
            StoredSagaInstances += instances.Count;
            return Task.CompletedTask;
        }
    }
}
