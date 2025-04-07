using System.Threading.Tasks;

namespace MyServiceBus;

internal sealed class RabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator
{
    public RabbitMqFactoryConfigurator()
    {
    }

    public string ClientHost { get; private set; } = "localhost";

    public void Message<T>(Action<MessageConfigurator> configure)
    {
        var configurator = new MessageConfigurator();
        configure(configurator);
    }

    public void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure)
    {
        var configurator = new ReceiveEndpointConfigurator();
        configure(configurator);
    }

    public void Host(string host)
    {
        ClientHost = host;
    }
}
