namespace MyServiceBus.Dashboard;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    public Uri MonitoringServiceAddress { get; set; } = new("http://localhost:5310");
    public int FailureSignalWindowSeconds { get; set; } = MonitoringWindow.DefaultSeconds;
    public DashboardFeatureOptions Features { get; } = new();
}

public sealed class DashboardFeatureOptions
{
    public bool Workflows { get; set; } = true;
    public bool Messages { get; set; }
}
