using MyServiceBus.Monitoring;

namespace MyServiceBus.Monitoring.Server;

public static class MonitoringApi
{
    public static IEndpointRouteBuilder MapMonitoringApi(this IEndpointRouteBuilder endpoints)
    {
        var ingest = endpoints.MapGroup("/api/monitoring/v1").WithTags("Monitoring ingest");
        ingest.MapPost("/metadata", (MonitoringMetadata metadata, MonitoringRepository repository, MonitoringChangeFeed changes) =>
        {
            repository.UpsertMetadata(metadata);
            changes.Publish("metadata_changed");
            return Results.Accepted();
        });
        ingest.MapPost("/observations:batch", (MonitoringObservationBatch batch, MonitoringRepository repository, MonitoringChangeFeed changes) =>
        {
            if (!repository.RecordBatch(batch))
                return Results.Conflict(new { error = "Metadata must be registered before observations are accepted." });
            changes.Publish("observations_changed");
            return Results.Accepted();
        });
        ingest.MapPost("/heartbeat", (MonitoringHeartbeat heartbeat, MonitoringRepository repository) =>
            repository.RecordHeartbeat(heartbeat) ? Results.Accepted() : Results.NotFound());

        var query = endpoints.MapGroup("/api/monitoring/v1").WithTags("Monitoring queries");
        query.MapGet("/applications", (MonitoringRepository repository) => repository.GetApplications(DateTimeOffset.UtcNow));
        query.MapGet("/instances", (string? application, MonitoringRepository repository) => repository.GetInstances(application, DateTimeOffset.UtcNow));
        query.MapGet("/metadata/{application}/{instanceId}/{busId}", (string application, string instanceId, string busId, MonitoringRepository repository) =>
        {
            var metadata = repository.GetMetadata(application, instanceId, busId);
            return metadata is null ? Results.NotFound() : Results.Ok(metadata);
        });
        query.MapGet("/observations", (string? application, int? limit, MonitoringRepository repository) =>
            repository.GetRecentObservations(application, limit ?? 100));
        query.MapGet("/stream", (HttpContext context, MonitoringChangeFeed changes, CancellationToken cancellationToken) =>
            changes.Stream(context, cancellationToken));

        return endpoints;
    }
}
