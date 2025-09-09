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

        services.AddSingleton<IReceiveEndpointConnector>([Throws(typeof(InvalidCastException), typeof(InvalidOperationException))] (sp) => (IReceiveEndpointConnector)sp.GetRequiredService<IMessageBus>());

        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));
        services.AddScoped<IRequestClientFactory, RequestClientFactory>();

        return services;
    }
}
