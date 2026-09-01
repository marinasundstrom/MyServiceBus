using MyServiceBus.Monitoring;

namespace MyServiceBus.Monitoring.Server;

public static class MonitoringApi
{
    public static IEndpointRouteBuilder MapMonitoringApi(this IEndpointRouteBuilder endpoints)
    {
        var ingest = endpoints.MapGroup("/api/monitoring/v1")
            .WithGroupName("v1")
            .WithTags("Monitoring ingest");
        ingest.MapPost("/metadata", async (
            MonitoringMetadata metadata,
            MonitoringIngestService ingestService,
            MonitoringChangeFeed changes,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await ingestService.UpsertMetadataAsync(metadata, cancellationToken);
                changes.Publish("metadata_changed");
                return Results.Accepted();
            }
            catch (MonitoringValidationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
            .WithSummary("Register or replace bus metadata for one application instance")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);
        ingest.MapPost("/observations:batch", async (
            MonitoringObservationBatch batch,
            MonitoringIngestService ingestService,
            MonitoringChangeFeed changes,
            CancellationToken cancellationToken) =>
        {
            if (!await ingestService.RecordBatchAsync(batch, cancellationToken))
                return Results.Conflict(new { error = "Metadata must be registered before observations are accepted." });
            changes.Publish("observations_changed");
            return Results.Accepted();
        })
            .WithSummary("Submit a sequenced batch of monitoring observations")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status409Conflict);
        ingest.MapPost("/heartbeat", async (
            MonitoringHeartbeat heartbeat,
            MonitoringIngestService ingestService,
            CancellationToken cancellationToken) =>
            await ingestService.RecordHeartbeatAsync(heartbeat, cancellationToken) ? Results.Accepted() : Results.NotFound())
            .WithSummary("Renew the monitoring lease for a registered application instance")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);
        ingest.MapPost("/scheduled-work", async (
            MonitoringScheduledWorkSnapshot snapshot,
            MonitoringIngestService ingestService,
            MonitoringChangeFeed changes,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!await ingestService.StoreScheduledWorkAsync(snapshot, cancellationToken))
                    return Results.Conflict(new { error = "Metadata must be registered before scheduled work is accepted." });
                changes.Publish("scheduled_work_changed");
                return Results.Accepted();
            }
            catch (MonitoringValidationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
            .WithSummary("Replace the current scheduled-work snapshot for an application instance")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);
        ingest.MapPost("/recurring-jobs", async (
            MonitoringRecurringJobSnapshot snapshot,
            MonitoringIngestService ingestService,
            MonitoringChangeFeed changes,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!await ingestService.StoreRecurringJobsAsync(snapshot, cancellationToken))
                    return Results.Conflict(new { error = "Metadata must be registered before recurring jobs are accepted." });
                changes.Publish("recurring_jobs_changed");
                return Results.Accepted();
            }
            catch (MonitoringValidationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
            .WithSummary("Replace the current recurring-job definitions for an application instance")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);
        ingest.MapPost("/jobs", async (
            MonitoringJobSnapshot snapshot,
            MonitoringIngestService ingestService,
            MonitoringChangeFeed changes,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!await ingestService.StoreJobsAsync(snapshot, cancellationToken))
                    return Results.Conflict(new { error = "Metadata must be registered before jobs are accepted." });
                changes.Publish("jobs_changed");
                return Results.Accepted();
            }
            catch (MonitoringValidationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
            .WithSummary("Replace the current tracked-job snapshot for an application instance")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        var query = endpoints.MapGroup("/api/monitoring/v1")
            .WithGroupName("v1")
            .WithTags("Monitoring queries");
        query.MapGet("/history", (MonitoringIngestService ingestService) => ingestService.GetHistory(DateTimeOffset.UtcNow))
            .WithSummary("Query monitoring storage durability and retained-window coverage");
        query.MapGet("/summary", (int? windowSeconds, MonitoringRepository repository) =>
            repository.GetDashboardSummary(windowSeconds ?? 60, DateTimeOffset.UtcNow))
            .WithSummary("Query a lightweight rolling operational summary for dashboard navigation")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(5)));
        query.MapGet("/applications", (MonitoringRepository repository) => repository.GetApplications(DateTimeOffset.UtcNow))
            .WithSummary("List application-level monitoring summaries");
        query.MapGet("/instances", (string? application, MonitoringRepository repository) => repository.GetInstances(application, DateTimeOffset.UtcNow))
            .WithSummary("List monitored application instances");
        query.MapGet("/endpoints", (string? application, int? windowSeconds, MonitoringRepository repository) =>
            repository.GetEndpoints(application, windowSeconds ?? 60, DateTimeOffset.UtcNow))
            .WithSummary("List receive endpoints with topology, availability, and activity");
        query.MapGet("/metadata/{application}/{instanceId}/{busId}", (string application, string instanceId, string busId, MonitoringRepository repository) =>
        {
            var metadata = repository.GetMetadata(application, instanceId, busId);
            return metadata is null ? Results.NotFound() : Results.Ok(metadata);
        })
            .WithSummary("Get the latest metadata for one application instance and bus")
            .Produces<MonitoringMetadata>()
            .Produces(StatusCodes.Status404NotFound);
        query.MapGet("/choreographies", (MonitoringRepository repository) =>
            repository.GetDeclaredChoreographies(DateTimeOffset.UtcNow))
            .WithSummary("List merged application-declared choreography fragments and conflicts")
            .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(5)));
        query.MapGet("/choreographies/runtime", (int? windowSeconds, MonitoringRepository repository) =>
            repository.GetChoreographyRuntime(windowSeconds ?? 300, DateTimeOffset.UtcNow))
            .WithSummary("Compare declared choreography reactions with exact causal observations");
        query.MapGet("/choreographies/runs", (string? choreography, int? windowSeconds, int? limit, MonitoringRepository repository) =>
            repository.GetChoreographyRuns(choreography, windowSeconds ?? 300, limit ?? 20, DateTimeOffset.UtcNow))
            .WithSummary("Reconstruct bounded declared choreography runs from exact causal observations");
        query.MapGet("/observations", (string? application, int? limit, MonitoringRepository repository) =>
            repository.GetRecentObservations(application, limit ?? 100))
            .WithSummary("List recent bounded monitoring observations");
        query.MapGet("/metrics", (string? application, int? windowSeconds, bool? byInstance, MonitoringRepository repository) =>
            repository.GetRates(application, windowSeconds ?? 60, byInstance ?? false, DateTimeOffset.UtcNow))
            .WithSummary("Query rates, counts, latency, retries, and failures for a time window");
        query.MapGet("/metrics/timeseries", (string? application, int? windowSeconds, int? bucketSeconds, bool? byInstance, MonitoringRepository repository) =>
            repository.GetTimeSeries(application, windowSeconds ?? 300, bucketSeconds ?? 5, byInstance ?? false, DateTimeOffset.UtcNow))
            .WithSummary("Query bucketed throughput and failure time series");
        query.MapGet("/flow", (string? application, int? windowSeconds, MonitoringRepository repository) =>
            repository.GetFlow(application, windowSeconds ?? 300, DateTimeOffset.UtcNow))
            .WithSummary("Query observed application message-flow paths");
        query.MapGet("/flow/replicas", (string? application, int? windowSeconds, MonitoringRepository repository) =>
            repository.GetReplicaFlow(application, windowSeconds ?? 300, DateTimeOffset.UtcNow))
            .WithSummary("Query observed message-flow paths between application replicas");
        query.MapGet("/flow/causal", (string? application, int? windowSeconds, MonitoringRepository repository) =>
            repository.GetCausalFlow(application, windowSeconds ?? 300, DateTimeOffset.UtcNow))
            .WithSummary("Query exact consumed-message to outgoing-operation reactions");
        query.MapGet("/outbox", (string? application, int? windowSeconds, MonitoringRepository repository) =>
            repository.GetOutboxDispatchers(application, windowSeconds ?? 60, DateTimeOffset.UtcNow))
            .WithSummary("Query outbox dispatcher state and windowed activity");
        query.MapGet("/scheduled-work", (string? application, string? status, MonitoringRepository repository) =>
            repository.GetScheduledWork(application, status, DateTimeOffset.UtcNow))
            .WithSummary("List current one-time scheduled work");
        query.MapGet("/recurring-jobs", (string? application, string? status, MonitoringRepository repository) =>
            repository.GetRecurringJobs(application, status, DateTimeOffset.UtcNow))
            .WithSummary("List current recurring-job definitions");
        query.MapGet("/jobs", (string? application, string? status, MonitoringRepository repository) =>
            repository.GetJobs(application, status, DateTimeOffset.UtcNow))
            .WithSummary("List current tracked jobs and bounded attempt history");
        query.MapGet("/stream", (HttpContext context, MonitoringChangeFeed changes, CancellationToken cancellationToken) =>
            changes.Stream(context, cancellationToken))
            .ExcludeFromDescription();

        return endpoints;
    }
}
