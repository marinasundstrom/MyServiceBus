namespace MyServiceBus;

public static class RabbitMqConfiguratorExtensions
{
    public static void ConfigureEndpoints(this IRabbitMqFactoryConfigurator configurator, IBusRegistrationContext context)
    {
        if (context is BusRegistrationContext busContext &&
            configurator is IReceiveConfigurator<ReceiveEndpointConfigurator> receiveConfigurator)
        {
            busContext.ConfigureEndpoints(receiveConfigurator);
        }
    }
}
