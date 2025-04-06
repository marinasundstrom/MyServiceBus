using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public sealed class ServiceBusHostedService : IHostedService
{
    private readonly IMessageBus messageBus;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<ServiceBusHostedService> logger;

    public ServiceBusHostedService(IMessageBus messageBus, IServiceProvider serviceProvider, ILogger<ServiceBusHostedService> logger)
    {
        this.messageBus = messageBus;
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var action in serviceProvider.GetServices<IPostBuildAction>())
        {
            action.Execute(serviceProvider);
        }

        messageBus.StartAsync(cancellationToken);

        logger.LogInformation("Hosted service started");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        messageBus.StopAsync(cancellationToken);

        logger.LogInformation("Hosted service stopped");

        return Task.CompletedTask;
    }
}