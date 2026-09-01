using Microsoft.AspNetCore.Components;
using MyServiceBus.Monitoring;

namespace MyServiceBus.Dashboard.Components.Pages;

public abstract class MonitoringPageBase : ComponentBase, IAsyncDisposable
{
    [Inject]
    protected MonitoringDashboardState Dashboard { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        Dashboard.Changed += OnDashboardChanged;
        await Dashboard.StartAsync();
    }

    protected static string GetGroup(MonitoringApplicationSummary application) => GetGroup(application.Labels);
    protected static string GetGroup(MonitoringInstanceSummary instance) => GetGroup(instance.Labels);

    protected static string GetGroup(IReadOnlyDictionary<string, string>? labels)
        => labels is not null && labels.TryGetValue("group", out var group) && !string.IsNullOrWhiteSpace(group)
            ? group
            : "Ungrouped";

    protected static long Failures(MonitoringCounterSet? counters)
        => counters is null ? 0 : counters.SendFaulted + counters.PublishFaulted + counters.ConsumeFaulted + counters.RetryExhausted;

    protected static string FormatRate(double? value) => value.HasValue ? value.Value.ToString("N2") : "—";
    protected static string FormatDuration(double? value) => value.HasValue ? $"{value.Value:N0} ms" : "—";
    protected static string FormatCount(long? value) => value.HasValue ? value.Value.ToString("N0") : "—";
    protected static string DisplayKind(string value) => value.Replace('_', ' ');
    protected static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    protected static string FormatAge(double? value) => value switch
    {
        null => "—",
        < 1_000 => $"{value.Value:N0} ms",
        < 60_000 => $"{value.Value / 1_000:N1} sec",
        _ => $"{value.Value / 60_000:N1} min"
    };

    protected static string RetryDescription(MonitoringObservation observation)
        => observation.RetryAttempt.HasValue
            ? $"Attempt {observation.RetryAttempt} · limit {observation.RetryLimit ?? 0}"
            : "—";

    protected static string ShortMessageType(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown message" : value.Split('.').Last();

    protected virtual void OnDashboardStateChanged()
    {
    }

    private void OnDashboardChanged()
    {
        _ = InvokeAsync(() =>
        {
            OnDashboardStateChanged();
            StateHasChanged();
        });
    }

    public ValueTask DisposeAsync()
    {
        Dashboard.Changed -= OnDashboardChanged;
        return ValueTask.CompletedTask;
    }
}
