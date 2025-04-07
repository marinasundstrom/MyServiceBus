

namespace MyServiceBus;

public interface IRabbitMqFactoryConfigurator
{
    void Message<T>(Action<MessageConfigurator> configure);
    void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure);
    void Host(string host);
}

public class MessageConfigurator
{
    public void SetEntityName(string name)
    {

    }
}

public class ReceiveEndpointConfigurator
{
    public void ConfigureConsumer<T>(IBusRegistrationContext context)
    {

    }
}