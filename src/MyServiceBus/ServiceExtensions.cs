using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class ServiceExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services, Action<IBusRegistrationConfigurator> configure)
    {
        var configurator = new BusRegistrationConfigurator(services);
        configure(configurator); // <-- you call AddConsumer, AddSaga, etc.
        //configurator.SetBusFactory(...); // set up transport like RabbitMQ

        // Registers bus and hosted service
        //configurator.CompleteRegistration();

        services.AddHostedService<ServiceBusHostedService>();

        return services;
    }
}
