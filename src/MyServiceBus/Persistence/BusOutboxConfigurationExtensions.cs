using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyServiceBus.Persistence;

public static class BusOutboxConfigurationExtensions
{
    public static void UseBusOutbox(this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        configurator.Services.TryAddScoped<OutboxSession>();
    }
}
