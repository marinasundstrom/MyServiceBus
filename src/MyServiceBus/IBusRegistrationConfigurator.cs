using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public interface IBusRegistrationConfigurator : IRegistrationConfigurator
{
    IServiceCollection Services { get; }

    void AddHook<THook>() where THook : class, IBusHook;
}
