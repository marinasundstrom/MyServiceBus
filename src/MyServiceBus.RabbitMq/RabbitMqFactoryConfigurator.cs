using System.Threading.Tasks;

namespace MyServiceBus;

internal sealed class RabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator
{
    public RabbitMqFactoryConfigurator()
    {
    }

    public string ClientHost { get; private set; } = "localhost";
    public string Password { get; internal set; }
    public string Username { get; internal set; }

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

    public void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null)
    {
        ClientHost = host;

        IRabbitMqHostConfigurator? configurator = new RabbitMqHostConfigurator(this);
        configure?.Invoke(configurator!);
    }
}

internal class RabbitMqHostConfigurator : IRabbitMqHostConfigurator
{
    private RabbitMqFactoryConfigurator rabbitMqFactoryConfigurator;

    public RabbitMqHostConfigurator(RabbitMqFactoryConfigurator rabbitMqFactoryConfigurator)
    {
        this.rabbitMqFactoryConfigurator = rabbitMqFactoryConfigurator;
    }

    public void Password(string password)
    {
        rabbitMqFactoryConfigurator.Password = password;
    }

    public void Username(string username)
    {
        rabbitMqFactoryConfigurator.Username = username;
    }
}