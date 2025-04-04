using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public interface IBusRegistrationConfigurator : IRegistrationConfigurator
{
}

public class BusRegistrationConfigurator : IBusRegistrationConfigurator
{
    private readonly IServiceCollection _services;

    public BusRegistrationConfigurator(IServiceCollection services)
    {
        _services = services;
    }

    public void AddConsumer<T>() where T : class, IConsumer
    {
        _services.AddScoped<T>();
        _services.AddScoped<IConsumer, T>();
        // Also tracks metadata internally for endpoint setup
    }
}
