using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyServiceBus;

public static class ServiceExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services, Action<IBusRegistrationConfigurator> configure)
    {
        var configurator = new BusRegistrationConfigurator(services);
        configure(configurator);

        configurator.Build();

        services.AddSingleton(typeof(IConsumerFactory<>), typeof(ScopeConsumerFactory<>));

        services.AddHostedService<ServiceBusHostedService>();

        services.AddSingleton<IReceiveEndpointConnector>((sp) => (IReceiveEndpointConnector)sp.GetRequiredService<IMessageBus>());

        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));
        services.AddScoped<IRequestClientFactory, RequestClientFactory>();
        services.TryAddSingleton<ILocalDelayScheduler, DefaultLocalDelayScheduler>();
        services.TryAddSingleton<IRecurringJobProvider>(provider => new InMemoryRecurringJobProvider(
            provider.GetRequiredService<IMessageBus>(),
            provider.GetRequiredService<ILocalDelayScheduler>()));
        services.TryAddSingleton<IRecurringJobSource>(provider =>
            (IRecurringJobSource)provider.GetRequiredService<IRecurringJobProvider>());
        services.TryAddSingleton<IRecurringJobScheduler, RecurringJobScheduler>();
        services.TryAddSingleton<InMemoryScheduledWorkSource>();
        services.TryAddSingleton<IScheduledWorkSource>(provider => provider.GetRequiredService<InMemoryScheduledWorkSource>());
        services.TryAddScoped<IScheduleMessageProvider, InMemoryScheduleMessageProvider>();
        services.AddScoped<IMessageScheduler, MessageScheduler>();

        return services;
    }
}
