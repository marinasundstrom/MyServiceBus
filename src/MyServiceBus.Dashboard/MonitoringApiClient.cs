using System.Net.Http.Json;
using MyServiceBus.Monitoring;

namespace MyServiceBus.Dashboard;

public sealed class MonitoringApiClient
{
    private readonly HttpClient httpClient;

    public MonitoringApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<MonitoringApplicationSummary>> GetApplications(CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringApplicationSummary[]>("/api/monitoring/v1/applications", cancellationToken).ConfigureAwait(false)
            ?? [];

    public async Task<IReadOnlyList<MonitoringInstanceSummary>> GetInstances(CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringInstanceSummary[]>("/api/monitoring/v1/instances", cancellationToken).ConfigureAwait(false)
            ?? [];
}
