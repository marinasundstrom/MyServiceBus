using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class ServiceExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services, Action<IBusRegistrationConfigurator> configure)
    {
        var configurator = new BusRegistrationConfigurator(services);
        configure(configurator); ;

        services.AddSingleton<IConsumerRegistry>(configurator.ConsumerRegistry);

        services.AddHostedService<ServiceBusHostedService>();

        return services;
    }
}
