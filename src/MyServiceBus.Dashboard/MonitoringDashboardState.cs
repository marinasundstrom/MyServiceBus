using System.Net.WebSockets;
using MyServiceBus.Monitoring;

namespace MyServiceBus.Dashboard;

public sealed class MonitoringDashboardState : IAsyncDisposable
{
    private readonly MonitoringApiClient api;
    private readonly CancellationTokenSource stopping = new();
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly SemaphoreSlim startLock = new(1, 1);
    private Task? pollTask;
    private Task? watchTask;
    private bool started;

    public MonitoringDashboardState(MonitoringApiClient api)
    {
        this.api = api;
    }

    public event Action? Changed;

    public IReadOnlyList<MonitoringApplicationSummary> Applications { get; private set; } = [];
    public MonitoringHistorySummary? History { get; private set; }
    public IReadOnlyList<MonitoringInstanceSummary> Instances { get; private set; } = [];
    public IReadOnlyList<MonitoringEndpointSummary> Endpoints { get; private set; } = [];
    public IReadOnlyList<MonitoringRateSummary> ApplicationRates { get; private set; } = [];
    public IReadOnlyList<MonitoringRateSummary> InstanceRates { get; private set; } = [];
    public IReadOnlyList<MonitoringTimeSeriesPoint> TimeSeries { get; private set; } = [];
    public IReadOnlyList<MonitoringFlowEdge> Flow { get; private set; } = [];
    public IReadOnlyList<MonitoringObservationRecord> Observations { get; private set; } = [];
    public IReadOnlyList<MonitoringOutboxDispatcherSummary> OutboxDispatchers { get; private set; } = [];
    public IReadOnlyList<MonitoringScheduledWorkSummary> ScheduledWork { get; private set; } = [];
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? Error { get; private set; }
    public bool LiveConnected { get; private set; }

    public async Task StartAsync()
    {
        await startLock.WaitAsync(stopping.Token);
        try
        {
            if (started)
                return;

            await RefreshAsync();
            pollTask = PollAsync();
            watchTask = WatchChangesAsync();
            started = true;
        }
        finally
        {
            startLock.Release();
        }
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stopping.Token))
                await RefreshAndNotifyAsync();
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
    }

    private async Task WatchChangesAsync()
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await foreach (var _ in api.WatchChanges(stopping.Token))
                {
                    LiveConnected = true;
                    await RefreshAndNotifyAsync();
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or WebSocketException or InvalidOperationException)
            {
                LiveConnected = false;
                Changed?.Invoke();
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stopping.Token);
        }
    }

    private async Task RefreshAndNotifyAsync()
    {
        await RefreshAsync();
        Changed?.Invoke();
    }

    private async Task RefreshAsync()
    {
        if (!await refreshLock.WaitAsync(0, stopping.Token))
            return;

        try
        {
            var applications = api.GetApplications(stopping.Token);
            var history = api.GetHistory(stopping.Token);
            var instances = api.GetInstances(stopping.Token);
            var endpoints = api.GetEndpoints(stopping.Token);
            var applicationRates = api.GetRates(false, stopping.Token);
            var instanceRates = api.GetRates(true, stopping.Token);
            var flow = api.GetFlow(stopping.Token);
            var timeSeries = api.GetTimeSeries(stopping.Token);
            var observations = api.GetRecentObservations(stopping.Token);
            var outboxDispatchers = api.GetOutboxDispatchers(stopping.Token);
            var scheduledWork = api.GetScheduledWork(stopping.Token);
            await Task.WhenAll(
                applications,
                history,
                instances,
                endpoints,
                applicationRates,
                instanceRates,
                flow,
                timeSeries,
                observations,
                outboxDispatchers,
                scheduledWork);
            Applications = applications.Result;
            History = history.Result;
            Instances = instances.Result;
            Endpoints = endpoints.Result;
            ApplicationRates = applicationRates.Result;
            InstanceRates = instanceRates.Result;
            Flow = flow.Result;
            TimeSeries = timeSeries.Result;
            Observations = observations.Result;
            OutboxDispatchers = outboxDispatchers.Result;
            ScheduledWork = scheduledWork.Result;
            UpdatedAt = DateTimeOffset.UtcNow;
            Error = null;
        }
        catch (HttpRequestException exception)
        {
            Error = exception.Message;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        stopping.Cancel();
        try
        {
            await Task.WhenAll(pollTask ?? Task.CompletedTask, watchTask ?? Task.CompletedTask);
        }
        catch (OperationCanceledException)
        {
        }

        stopping.Dispose();
        refreshLock.Dispose();
        startLock.Dispose();
    }
}
