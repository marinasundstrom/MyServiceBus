using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using MyServiceBus.Monitoring;

namespace MyServiceBus.Dashboard;

public sealed class MonitoringDashboardState : IAsyncDisposable
{
    private readonly MonitoringApiClient api;
    private readonly DashboardOptions options;
    private readonly CancellationTokenSource stopping = new();
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly SemaphoreSlim startLock = new(1, 1);
    private Task? pollTask;
    private Task? watchTask;
    private bool started;

    public MonitoringDashboardState(MonitoringApiClient api, IOptions<DashboardOptions> options)
    {
        this.api = api;
        this.options = options.Value;
    }

    public event Action? Changed;

    public IReadOnlyList<MonitoringApplicationSummary> Applications { get; private set; } = [];
    public MonitoringHistorySummary? History { get; private set; }
    public MonitoringDashboardSummary? Summary { get; private set; }
    public IReadOnlyList<MonitoringInstanceSummary> Instances { get; private set; } = [];
    public IReadOnlyList<MonitoringEndpointSummary> Endpoints { get; private set; } = [];
    public IReadOnlyList<MonitoringRateSummary> ApplicationRates { get; private set; } = [];
    public IReadOnlyList<MonitoringRateSummary> InstanceRates { get; private set; } = [];
    public IReadOnlyList<MonitoringTimeSeriesPoint> TimeSeries { get; private set; } = [];
    public IReadOnlyList<MonitoringFlowEdge> Flow { get; private set; } = [];
    public IReadOnlyList<MonitoringRequestResponseExchange> RequestResponseExchanges { get; private set; } = [];
    public IReadOnlyList<MonitoringReplicaFlowEdge> ReplicaFlow { get; private set; } = [];
    public IReadOnlyList<MonitoringDeclaredChoreography> Choreographies { get; private set; } = [];
    public IReadOnlyList<MonitoringDeclaredSagaStateMachine> Sagas { get; private set; } = [];
    public IReadOnlyList<MonitoringWorkflowCatalogItem> WorkflowCatalog { get; private set; } = [];
    public MonitoringChoreographyRuntimeSnapshot? ChoreographyRuntime { get; private set; }
    public MonitoringWorkflowRunPage? WorkflowRuns { get; private set; }
    public MonitoringWorkflowRunIndexPage? WorkflowRunIndex { get; private set; }
    public IReadOnlyList<MonitoringObservationRecord> Observations { get; private set; } = [];
    public MonitoringMessageIndexPage? MessageIndex { get; private set; }
    public IReadOnlyList<MonitoringOutboxDispatcherSummary> OutboxDispatchers { get; private set; } = [];
    public IReadOnlyList<MonitoringScheduledWorkSummary> ScheduledWork { get; private set; } = [];
    public IReadOnlyList<MonitoringRecurringJobSummary> RecurringJobs { get; private set; } = [];
    public IReadOnlyList<MonitoringJobSummary> Jobs { get; private set; } = [];
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
            Changed?.Invoke();
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
            var summary = api.GetSummary(stopping.Token);
            var instances = api.GetInstances(stopping.Token);
            var endpoints = api.GetEndpoints(stopping.Token);
            var applicationRates = api.GetRates(false, stopping.Token);
            var instanceRates = api.GetRates(true, stopping.Token);
            var flow = api.GetFlow(stopping.Token);
            var requestResponseExchanges = api.GetRequestResponseExchanges(stopping.Token);
            var replicaFlow = api.GetReplicaFlow(stopping.Token);
            var choreographies = options.Features.Workflows
                ? api.GetChoreographies(stopping.Token)
                : Task.FromResult<IReadOnlyList<MonitoringDeclaredChoreography>>([]);
            var sagas = options.Features.Workflows
                ? api.GetSagas(stopping.Token)
                : Task.FromResult<IReadOnlyList<MonitoringDeclaredSagaStateMachine>>([]);
            var workflowCatalog = options.Features.Workflows
                ? api.GetWorkflowCatalog(stopping.Token)
                : Task.FromResult<IReadOnlyList<MonitoringWorkflowCatalogItem>>([]);
            var choreographyRuntime = options.Features.Workflows
                ? api.GetChoreographyRuntime(stopping.Token)
                : Task.FromResult<MonitoringChoreographyRuntimeSnapshot?>(null);
            var workflowRuns = options.Features.Workflows
                ? api.GetWorkflowRuns(null, null, null, null, null, null, 0, 100, stopping.Token)
                : Task.FromResult<MonitoringWorkflowRunPage?>(null);
            var workflowRunIndex = options.Features.Workflows
                ? api.GetWorkflowRunIndex(null, null, null, null, 0, 100, stopping.Token)
                : Task.FromResult<MonitoringWorkflowRunIndexPage?>(null);
            var timeSeries = api.GetTimeSeries(stopping.Token);
            var observations = api.GetRecentObservations(stopping.Token);
            var messages = options.Features.Messages
                ? api.GetMessages(null, null, null, 0, 25, stopping.Token)
                : Task.FromResult<MonitoringMessageIndexPage?>(null);
            var outboxDispatchers = api.GetOutboxDispatchers(stopping.Token);
            var scheduledWork = api.GetScheduledWork(stopping.Token);
            var recurringJobs = api.GetRecurringJobs(stopping.Token);
            var jobs = api.GetJobs(stopping.Token);
            await Task.WhenAll(
                applications,
                history,
                summary,
                instances,
                endpoints,
                applicationRates,
                instanceRates,
                flow,
                requestResponseExchanges,
                replicaFlow,
                choreographies,
                sagas,
                workflowCatalog,
                choreographyRuntime,
                workflowRuns,
                workflowRunIndex,
                timeSeries,
                observations,
                messages,
                outboxDispatchers,
                scheduledWork,
                recurringJobs,
                jobs);
            Applications = applications.Result;
            History = history.Result;
            Summary = summary.Result;
            Instances = instances.Result;
            Endpoints = endpoints.Result;
            ApplicationRates = applicationRates.Result;
            InstanceRates = instanceRates.Result;
            Flow = flow.Result;
            RequestResponseExchanges = requestResponseExchanges.Result;
            ReplicaFlow = replicaFlow.Result;
            Choreographies = choreographies.Result;
            Sagas = sagas.Result;
            WorkflowCatalog = workflowCatalog.Result;
            ChoreographyRuntime = choreographyRuntime.Result;
            WorkflowRuns = workflowRuns.Result;
            WorkflowRunIndex = workflowRunIndex.Result;
            TimeSeries = timeSeries.Result;
            Observations = observations.Result;
            MessageIndex = messages.Result;
            OutboxDispatchers = outboxDispatchers.Result;
            ScheduledWork = scheduledWork.Result;
            RecurringJobs = recurringJobs.Result;
            Jobs = jobs.Result;
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
