using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public interface IBusRegistrationConfigurator : IRegistrationConfigurator
{
    IServiceCollection Services { get; }
}