namespace MyServiceBus.Monitoring;

public enum MonitoringCaptureProfile
{
    Auto,
    Development,
    Production
}

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
    public int MaxScheduledWorkItems { get; set; } = 1_000;
    public int MaxJobItems { get; set; } = 1_000;
    public int MaxJobAttempts { get; set; } = 10;
    public TimeSpan ScheduledWorkHistory { get; set; } = TimeSpan.FromHours(24);
    public MonitoringCaptureProfile CaptureProfile { get; set; } = MonitoringCaptureProfile.Auto;
    public bool? CaptureMessageIdentity { get; set; }
    public bool? CaptureCorrelationIdentity { get; set; }
    public bool? CaptureRequestResponseMetadata { get; set; }
    public bool? CaptureAddresses { get; set; }
    public bool? CaptureExceptionMessages { get; set; }

    internal bool CaptureSensitiveData(bool? value)
    {
        if (value.HasValue)
            return value.Value;

        return CaptureProfile switch
        {
            MonitoringCaptureProfile.Development => true,
            MonitoringCaptureProfile.Production => false,
            _ => IsDevelopmentEnvironment()
        };
    }

    private static bool IsDevelopmentEnvironment()
    {
        var environment = Environment.GetEnvironmentVariable("MYSERVICEBUS_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
