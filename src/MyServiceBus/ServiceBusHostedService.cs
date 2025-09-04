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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var action in serviceProvider.GetServices<IPostBuildAction>())
        {
            action.Execute(serviceProvider);
        }

        await messageBus.StartAsync(cancellationToken);

        logger.LogInformation("🚀 Service bus started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await messageBus.StopAsync(cancellationToken);

        logger.LogInformation("🛑 Service bus stopped");
    }
}