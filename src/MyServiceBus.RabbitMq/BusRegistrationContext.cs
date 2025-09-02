


namespace MyServiceBus;

internal sealed class BusRegistrationContext : IBusRegistrationContext
{
    public BusRegistrationContext(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
}
