namespace MyServiceBus;

public sealed class BusRegistrationContext : IBusRegistrationContext
{
    public BusRegistrationContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IServiceProvider ServiceProvider { get; }
}
