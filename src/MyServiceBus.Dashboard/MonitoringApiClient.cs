using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
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

    public async Task<IReadOnlyList<MonitoringEndpointSummary>> GetEndpoints(CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringEndpointSummary[]>(
            "/api/monitoring/v1/endpoints?windowSeconds=60",
            cancellationToken).ConfigureAwait(false)
            ?? [];

    public async Task<IReadOnlyList<MonitoringRateSummary>> GetRates(bool byInstance, CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringRateSummary[]>(
            $"/api/monitoring/v1/metrics?windowSeconds=60&byInstance={byInstance.ToString().ToLowerInvariant()}",
            cancellationToken).ConfigureAwait(false)
            ?? [];

    public async Task<IReadOnlyList<MonitoringFlowEdge>> GetFlow(CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringFlowEdge[]>(
            "/api/monitoring/v1/flow?windowSeconds=300",
            cancellationToken).ConfigureAwait(false)
            ?? [];

    public async Task<IReadOnlyList<MonitoringTimeSeriesPoint>> GetTimeSeries(CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringTimeSeriesPoint[]>(
            "/api/monitoring/v1/metrics/timeseries?windowSeconds=300&bucketSeconds=5",
            cancellationToken).ConfigureAwait(false)
            ?? [];

    public async Task<IReadOnlyList<MonitoringObservationRecord>> GetRecentObservations(CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringObservationRecord[]>(
            "/api/monitoring/v1/observations?limit=100",
            cancellationToken).ConfigureAwait(false)
            ?? [];

    public async Task<IReadOnlyList<MonitoringOutboxDispatcherSummary>> GetOutboxDispatchers(
        CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<MonitoringOutboxDispatcherSummary[]>(
            "/api/monitoring/v1/outbox?windowSeconds=60",
            cancellationToken).ConfigureAwait(false)
            ?? [];

    public async IAsyncEnumerable<string> WatchChanges(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var baseAddress = httpClient.BaseAddress
            ?? throw new InvalidOperationException("The monitoring service address is not configured.");
        var streamAddress = new UriBuilder(baseAddress)
        {
            Scheme = baseAddress.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = "/api/monitoring/v1/stream"
        }.Uri;

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(streamAddress, cancellationToken).ConfigureAwait(false);
        yield return "{\"type\":\"connected\"}";
        var buffer = new byte[4 * 1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var message = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    yield break;
                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            yield return message.ToString();
        }
    }
}
