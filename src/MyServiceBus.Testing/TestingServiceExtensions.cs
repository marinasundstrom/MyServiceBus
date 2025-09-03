using System;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class TestingServiceExtensions
{
    public static IServiceCollection AddServiceBusTestHarness(this IServiceCollection services, Action<IBusRegistrationConfigurator> configure)
    {
        var configurator = new BusRegistrationConfigurator(services);
        configure(configurator);

        configurator.Build();

        services.AddSingleton<InMemoryTestHarness>();
        services.AddSingleton<IMessageBus>([Throws(typeof(InvalidOperationException))] sp => sp.GetRequiredService<InMemoryTestHarness>());
        services.AddSingleton<ITransportFactory>([Throws(typeof(InvalidOperationException))] sp => sp.GetRequiredService<InMemoryTestHarness>());
        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));

        return services;
    }
}
