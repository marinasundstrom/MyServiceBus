namespace MyServiceBus;

public interface IRabbiqMqFactoryConfigurator
{
    void ConfigureEndpoints(IBusRegistrationContext context);
}