

namespace MyServiceBus;

public interface IRabbitMqFactoryConfigurator
{
    void Message<T>(Action<MessageConfigurator> configure);
    void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure);
    void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null);
}

public interface IRabbitMqHostConfigurator
{
    void Username(string username);
    void Password(string password);
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