using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyServiceBus.Inspection;

namespace TestApp;

public static class DashboardApi
{
    public static RouteGroupBuilder MapDashboardApi(
        this IEndpointRouteBuilder endpoints,
        DashboardMetadata metadata,
        string prefix = "/inspection/v1")
    {
        var group = endpoints.MapGroup(prefix)
            .WithTags("Dashboard");

        group.MapGet("/overview", (IBusInspectionProvider inspectionProvider, DashboardState state) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateOverview(inspectionProvider, metadata, state)));

        group.MapGet("/messages", (IBusInspectionProvider inspectionProvider) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateMessages(inspectionProvider, metadata)));

        group.MapGet("/consumers", (IBusInspectionProvider inspectionProvider) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateConsumers(inspectionProvider, metadata)));

        group.MapGet("/topology", (IBusInspectionProvider inspectionProvider) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateTopology(inspectionProvider, metadata)));

        group.MapGet("/queues", (IBusInspectionProvider inspectionProvider) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateQueues(inspectionProvider, metadata)));

        group.MapGet("/metrics", (IBusInspectionProvider inspectionProvider, DashboardState state) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateMetrics(inspectionProvider, metadata, state)));

        return group;
    }
}

public sealed record DashboardMetadata(string ServiceName, string TransportName);
