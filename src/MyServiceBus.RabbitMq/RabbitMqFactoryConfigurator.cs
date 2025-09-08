namespace MyServiceBus;

using System;
using System.Collections.Generic;

internal sealed class RabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator
{
    private readonly Dictionary<Type, string> _exchangeNames = new();
    private readonly List<Action<IMessageBus, IServiceProvider>> _endpointActions = new();
    private IEndpointNameFormatter? _endpointNameFormatter;
    private IMessageEntityNameFormatter? _entityNameFormatter;
    public ushort PrefetchCount { get; private set; }

    public RabbitMqFactoryConfigurator()
    {
    }

    public string ClientHost { get; private set; } = "localhost";
    public string Password { get; internal set; }
    public string Username { get; internal set; }
    public IEndpointNameFormatter? EndpointNameFormatter => _endpointNameFormatter;
    public IMessageEntityNameFormatter? EntityNameFormatter => _entityNameFormatter;

    public void Message<T>(Action<MessageConfigurator> configure)
    {
        var configurator = new MessageConfigurator(typeof(T), _exchangeNames);
        configure(configurator);
    }

    public void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure)
    {
        var configurator = new ReceiveEndpointConfigurator(queueName, _exchangeNames, _endpointActions);
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

    public void SetEntityNameFormatter(IMessageEntityNameFormatter formatter)
    {
        _entityNameFormatter = formatter;
        NamingConventions.SetEntityNameFormatter(formatter);
    }

    public void SetPrefetchCount(ushort prefetchCount)
    {
        PrefetchCount = prefetchCount;
    }

    internal void Apply(IMessageBus bus, IServiceProvider provider)
    {
        foreach (var action in _endpointActions)
            action(bus, provider);
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