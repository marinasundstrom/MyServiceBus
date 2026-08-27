namespace MyServiceBus.Monitoring;

public sealed class MonitoringExporterOptions
{
    public required Uri ServiceAddress { get; set; }
    public required string ApplicationName { get; set; }
    public string InstanceId { get; set; } = $"{Environment.MachineName}-{Environment.ProcessId}";
    public string ApplicationVersion { get; set; } = "unknown";
    public string BusId { get; set; } = "bus";
    public IDictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
    public int MaxBatchSize { get; set; } = 256;
    public int MaxQueueSize { get; set; } = 10_000;
}
