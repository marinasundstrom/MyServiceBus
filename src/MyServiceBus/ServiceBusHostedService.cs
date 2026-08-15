using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public sealed class ServiceBusHostedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<ServiceBusHostedService> logger;
    private IMessageBus? messageBus;

    public ServiceBusHostedService(IServiceProvider serviceProvider, ILogger<ServiceBusHostedService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var action in serviceProvider.GetServices<IPostBuildAction>())
        {
            action.Execute(serviceProvider);
        }

        messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        await messageBus.StartAsync(cancellationToken);

        logger.LogInformation("🚀 Service bus started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (messageBus is not null)
            await messageBus.StopAsync(cancellationToken);

        logger.LogInformation("🛑 Service bus stopped");
    }
}
