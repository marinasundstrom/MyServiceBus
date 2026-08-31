namespace MyServiceBus.Monitoring.Server;

public sealed class MonitoringStorageOptions
{
    public const string SectionName = "Monitoring:Storage";

    public string Provider { get; set; } = "InMemory";
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
    public string ConnectionStringName { get; set; } = "Monitoring";
}
