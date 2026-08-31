using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyServiceBus.Inspection;

namespace MyServiceBus.Monitoring;

public static class MonitoringServiceCollectionExtensions
{
    private const string HttpClientName = "MyServiceBus.Monitoring";

    public static IServiceCollection AddServiceBusMonitoring(
        this IServiceCollection services,
        Action<MonitoringExporterOptions> configure)
    {
        var options = new MonitoringExporterOptions
        {
            ServiceAddress = new Uri("http://localhost:5310"),
            ApplicationName = "MyServiceBus.Application"
        };
        configure(options);

        if (options.ExportInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(configure), "ExportInterval must be greater than zero.");
        if (options.HeartbeatInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(configure), "HeartbeatInterval must be greater than zero.");
        if (options.MaxBatchSize <= 0 || options.MaxQueueSize <= 0 || options.MaxScheduledWorkItems <= 0
            || options.MaxJobItems <= 0 || options.MaxJobAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(configure), "Batch and queue sizes must be greater than zero.");
        if (options.ScheduledWorkHistory <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(configure), "ScheduledWorkHistory must be greater than zero.");

        services.TryAddSingleton<IBusInspectionProvider, BusInspectionProvider>();
        services.AddSingleton(options);
        services.AddHttpClient(HttpClientName, client => client.BaseAddress = options.ServiceAddress);
        services.AddSingleton(sp => new MonitoringExporter(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            sp,
            options,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MonitoringExporter>>()));
        services.AddSingleton<IBusHook>(sp => sp.GetRequiredService<MonitoringExporter>());
        services.AddSingleton<IScheduledWorkObserver>(sp => sp.GetRequiredService<MonitoringExporter>());
        services.AddHostedService(sp => sp.GetRequiredService<MonitoringExporter>());
        return services;
    }
}
