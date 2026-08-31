using Microsoft.AspNetCore.Components;
using MyServiceBus.Monitoring;

namespace MyServiceBus.Dashboard.Components.Pages;

public abstract class ApplicationPageBase : MonitoringPageBase
{
    [Parameter]
    public string ApplicationName { get; set; } = string.Empty;

    protected MonitoringApplicationSummary? Application
        => Dashboard.Applications.FirstOrDefault(application => string.Equals(
            application.ApplicationName,
            ApplicationName,
            StringComparison.Ordinal));

    protected MonitoringRateSummary? Rate
        => Dashboard.ApplicationRates.FirstOrDefault(rate => string.Equals(
            rate.ApplicationName,
            ApplicationName,
            StringComparison.Ordinal));

    protected IReadOnlyList<MonitoringInstanceSummary> ApplicationInstances
        => Dashboard.Instances
            .Where(instance => string.Equals(instance.ApplicationName, ApplicationName, StringComparison.Ordinal))
            .OrderByDescending(instance => instance.Online)
            .ThenBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .ToArray();

    protected IReadOnlyList<MonitoringEndpointSummary> ApplicationEndpoints
        => Dashboard.Endpoints
            .Where(endpoint => string.Equals(endpoint.ApplicationName, ApplicationName, StringComparison.Ordinal))
            .OrderByDescending(endpoint => endpoint.ConsumedPerSecond)
            .ThenBy(endpoint => endpoint.EndpointName, StringComparer.Ordinal)
            .ToArray();

    protected IReadOnlyList<MonitoringTimeSeriesPoint> ApplicationTimeSeries
        => Dashboard.TimeSeries
            .Where(point => string.Equals(point.ApplicationName, ApplicationName, StringComparison.Ordinal))
            .ToArray();

    protected IReadOnlyList<MonitoringFlowEdge> ApplicationFlow
        => Dashboard.Flow
            .Where(edge => string.Equals(edge.SourceApplication, ApplicationName, StringComparison.Ordinal)
                || string.Equals(edge.TargetApplication, ApplicationName, StringComparison.Ordinal))
            .ToArray();

    protected IReadOnlyList<MonitoringReplicaFlowEdge> ApplicationReplicaFlow
        => Dashboard.ReplicaFlow
            .Where(edge => string.Equals(edge.SourceApplication, ApplicationName, StringComparison.Ordinal)
                || string.Equals(edge.TargetApplication, ApplicationName, StringComparison.Ordinal))
            .ToArray();

    protected IReadOnlyList<MonitoringInstanceSummary> ReplicaFlowInstances
    {
        get
        {
            var identities = ApplicationReplicaFlow
                .SelectMany(edge => new[]
                {
                    (edge.SourceApplication, edge.SourceInstanceId, edge.SourceBusId),
                    (edge.TargetApplication, edge.TargetInstanceId, edge.TargetBusId)
                })
                .ToHashSet();
            return Dashboard.Instances
                .Where(instance => string.Equals(instance.ApplicationName, ApplicationName, StringComparison.Ordinal)
                    || identities.Contains((instance.ApplicationName, instance.InstanceId, instance.BusId)))
                .ToArray();
        }
    }

    protected IReadOnlyList<MonitoringApplicationSummary> FlowApplications
    {
        get
        {
            var names = ApplicationFlow
                .SelectMany(edge => new[] { edge.SourceApplication, edge.TargetApplication })
                .Append(ApplicationName)
                .ToHashSet(StringComparer.Ordinal);
            return Dashboard.Applications
                .Where(application => names.Contains(application.ApplicationName))
                .ToArray();
        }
    }

    protected IReadOnlyList<ApplicationBusSummary> Buses
        => ApplicationInstances
            .GroupBy(instance => new
            {
                instance.BusId,
                instance.TransportName,
                instance.BusAddress
            })
            .Select(group => new ApplicationBusSummary(
                group.Key.BusId,
                group.Key.TransportName,
                group.Key.BusAddress,
                group.Count(instance => instance.Online),
                group.Count()))
            .OrderBy(bus => bus.BusId, StringComparer.Ordinal)
            .ToArray();

    protected sealed record ApplicationBusSummary(
        string BusId,
        string TransportName,
        string BusAddress,
        int OnlineInstances,
        int TotalInstances);
}
