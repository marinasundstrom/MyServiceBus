using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyServiceBus;

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

        group.MapGet("/overview", (IMessageBus bus, DashboardState state) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateOverview(bus, metadata, state)));

        group.MapGet("/messages", (IMessageBus bus) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateMessages(bus, metadata)));

        group.MapGet("/consumers", (IMessageBus bus) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateConsumers(bus, metadata)));

        group.MapGet("/topology", (IMessageBus bus) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateTopology(bus, metadata)));

        group.MapGet("/queues", (IMessageBus bus) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateQueues(bus, metadata)));

        group.MapGet("/metrics", (IMessageBus bus, DashboardState state) =>
            TypedResults.Ok(DashboardSnapshotFactory.CreateMetrics(bus, metadata, state)));

        return group;
    }
}

public sealed record DashboardMetadata(string ServiceName, string TransportName);
