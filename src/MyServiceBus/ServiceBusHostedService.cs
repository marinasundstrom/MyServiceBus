using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public sealed class ServiceBusHostedService : IHostedService
{
    private readonly ILogger<ServiceBusHostedService> logger;

    public ServiceBusHostedService(ILogger<ServiceBusHostedService> logger)
    {
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Hosted service started");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Hosted service stopped");

        return Task.CompletedTask;
    }
}