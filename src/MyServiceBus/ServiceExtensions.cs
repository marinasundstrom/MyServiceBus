using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class ServiceExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services, Action<IBusRegistrationConfigurator> configuration)
    {
        services.AddHostedService<ServiceBusHostedService>();

        return services;
    }
}
