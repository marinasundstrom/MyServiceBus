namespace MyServiceBus.Dashboard;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    public Uri MonitoringServiceAddress { get; set; } = new("http://localhost:5310");
}
