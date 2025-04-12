using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class ServiceExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services, Action<IBusRegistrationConfigurator> configure)
    {
        var configurator = new BusRegistrationConfigurator(services);
        configure(configurator);

        configurator.Build();

        services.AddHostedService<ServiceBusHostedService>();

        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));

        return services;
    }
}
