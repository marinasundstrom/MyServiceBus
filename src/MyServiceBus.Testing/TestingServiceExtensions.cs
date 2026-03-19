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
        services.AddSingleton(typeof(IConsumerFactory<>), typeof(ScopeConsumerFactory<>));
        services.AddSingleton<IMessageBus>((sp) => sp.GetRequiredService<InMemoryTestHarness>());
        services.AddSingleton<ITransportFactory>((sp) => sp.GetRequiredService<InMemoryTestHarness>());
        services.AddSingleton<IReceiveEndpointConnector>((sp) => (IReceiveEndpointConnector)sp.GetRequiredService<IMessageBus>());
        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));
        services.AddScoped<IRequestClientFactory, RequestClientFactory>();

        return services;
    }
}
