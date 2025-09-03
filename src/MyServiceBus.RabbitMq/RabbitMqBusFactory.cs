using Microsoft.Extensions.DependencyInjection;
using System;

namespace MyServiceBus;

/// <summary>
/// Configures the service bus to use RabbitMQ transport.
/// Mirrors the MassTransit bus factory pattern and populates
/// the supplied <see cref="IServiceCollection"/>.
/// </summary>
public static class RabbitMqBusFactory
{
    public static void Configure(
        IServiceCollection services,
        Action<IBusRegistrationConfigurator>? configureBus = null,
        Action<IBusRegistrationContext, IRabbitMqFactoryConfigurator>? configure = null)
    {
        var configurator = new BusRegistrationConfigurator(services);

        configureBus?.Invoke(configurator);

        configurator.UsingRabbitMq((context, cfg) =>
        {
            configure?.Invoke(context, cfg);
        });

        configurator.Build();

        services.AddHostedService<ServiceBusHostedService>();
        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));
    }
}
