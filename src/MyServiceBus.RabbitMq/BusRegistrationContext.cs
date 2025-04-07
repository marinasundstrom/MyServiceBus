


namespace MyServiceBus;

internal sealed class BusRegistrationContext : IBusRegistrationContext
{
    public BusRegistrationContext(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    public void ConfigureEndpoints<T>(IReceiveConfigurator<T> configurator, IEndpointNameFormatter endpointNameFormatter = null)
            where T : IReceiveEndpointConfigurator
    {

    }
}

public interface IReceiveEndpointConfigurator
{
}

public interface IEndpointNameFormatter
{
}

public interface IReceiveConfigurator<T>
{
}