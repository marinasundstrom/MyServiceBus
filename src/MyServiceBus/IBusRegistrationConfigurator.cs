using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Choreography;

namespace MyServiceBus;

public interface IBusRegistrationConfigurator : IRegistrationConfigurator
{
    IServiceCollection Services { get; }

    void AddChoreography(ChoreographyFragment fragment);

    void AddHook<THook>() where THook : class, IBusHook;
}
