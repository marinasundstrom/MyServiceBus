using MyServiceBus.Inspection;
using MyServiceBus;
using MyServiceBus.Monitoring;
using MyServiceBus.Monitoring.Server;
using Shouldly;

public class MonitoringRepositoryTests
{
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
                [])));

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
        repository.GetRecentObservations("orders", 10).ShouldHaveSingleItem();
    }

    private sealed record TestMessage(string Value);
}
