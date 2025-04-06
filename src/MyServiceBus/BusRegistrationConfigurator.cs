using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public class BusRegistrationConfigurator : IBusRegistrationConfigurator
{
    public IConsumerRegistry ConsumerRegistry { get; } = new ConsumerRegistry();

    public IServiceCollection Services { get; }

    public BusRegistrationConfigurator(IServiceCollection services)
    {
        Services = services;
    }

    public void AddConsumer<T>() where T : class, IConsumer
    {
        Services.AddScoped<T>();
        Services.AddScoped<IConsumer, T>();

        // Also tracks metadata internally for endpoint setup
        ConsumerRegistry.Register(typeof(T));
    }
}
