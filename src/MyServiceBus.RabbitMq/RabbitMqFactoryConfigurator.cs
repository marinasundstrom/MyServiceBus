namespace MyServiceBus;

using System;
using System.Collections.Generic;

internal sealed class RabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator
{
    private readonly Dictionary<Type, string> _exchangeNames = new();
    private IEndpointNameFormatter? _endpointNameFormatter;

    public RabbitMqFactoryConfigurator()
    {
    }

    public string ClientHost { get; private set; } = "localhost";
    public string Password { get; internal set; }
    public string Username { get; internal set; }
    public IEndpointNameFormatter? EndpointNameFormatter => _endpointNameFormatter;

    public void Message<T>(Action<MessageConfigurator> configure)
    {
        var configurator = new MessageConfigurator(typeof(T), _exchangeNames);
        configure(configurator);
    }

    public void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure)
    {
        var configurator = new ReceiveEndpointConfigurator(queueName, _exchangeNames);
        configure(configurator);
    }

    public void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null)
    {
        ClientHost = host;

        IRabbitMqHostConfigurator? configurator = new RabbitMqHostConfigurator(this);
        configure?.Invoke(configurator!);
    }

    public void SetEndpointNameFormatter(IEndpointNameFormatter formatter)
    {
        _endpointNameFormatter = formatter;
    }
}

internal class RabbitMqHostConfigurator : IRabbitMqHostConfigurator
{
    private readonly RabbitMqFactoryConfigurator rabbitMqFactoryConfigurator;

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