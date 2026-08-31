using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using MyServiceBus.Inspection;
using MyServiceBus.Monitoring;
using MyServiceBus.Monitoring.Server;
using Testcontainers.PostgreSql;

namespace MyServiceBus.PostgreSql.Tests;

public sealed class PostgreSqlMonitoringHistoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
    private PooledDbContextFactory<MonitoringHistoryDbContext> contextFactory = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var contextOptions = new DbContextOptionsBuilder<MonitoringHistoryDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        contextFactory = new PooledDbContextFactory<MonitoringHistoryDbContext>(contextOptions);
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Persists_and_restores_monitoring_ingest_records()
    {
        var now = DateTimeOffset.UtcNow;
        var options = Options.Create(new MonitoringStorageOptions
        {
            Provider = "PostgreSql",
            Retention = TimeSpan.FromDays(7)
        });
        var first = new PostgreSqlMonitoringHistoryStore(contextFactory, options);
        await first.InitializeAsync(CancellationToken.None);
        await first.InitializeAsync(CancellationToken.None);

        var metadata = new MonitoringMetadata(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "1.0.0",
            "dotnet",
            "1.0.0",
            "bus",
            now.AddMinutes(-1),
            now,
            new BusInspectionSnapshot("rabbitmq", new Uri("rabbitmq://localhost/"), now, [], [], []));
        var batch = new MonitoringObservationBatch(
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
        var heartbeat = new MonitoringHeartbeat(
            MonitoringProtocol.Version,
            "orders",
            "orders-1",
            "bus",
            now);

        await first.StoreMetadataAsync(metadata, CancellationToken.None);
        await first.StoreBatchAsync(batch, CancellationToken.None);
        await first.StoreBatchAsync(batch, CancellationToken.None);
        await first.StoreHeartbeatAsync(heartbeat, CancellationToken.None);

        var restarted = new PostgreSqlMonitoringHistoryStore(contextFactory, options);
        await restarted.InitializeAsync(CancellationToken.None);
        var restored = await restarted.RestoreAsync(now.AddMinutes(-15), CancellationToken.None);

        Assert.True(restarted.Durable);
        Assert.Equal("PostgreSql", restarted.Provider);
        Assert.NotNull(restarted.HistoryAvailableFromUtc);
        Assert.Single(restored.Metadata);
        Assert.Single(restored.Batches);
        Assert.Single(restored.Heartbeats);
        Assert.Equal("batch-1", restored.Batches[0].BatchId);
        Assert.NotNull(restored.LastIngestAtUtc);
    }
}
