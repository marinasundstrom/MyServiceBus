namespace MyServiceBus;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using RabbitMQ.Client;

public class RabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator, IBusFactoryConfigurator
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
        MyServiceBus.EntityNameFormatter.SetFormatter(formatter);
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

    public IMessageBus Build()
    {
        var services = new ServiceCollection();
        Configure(services);
        var provider = services.BuildServiceProvider();
        foreach (var action in provider.GetServices<IPostBuildAction>())
            action.Execute(provider);
        return provider.GetRequiredService<IMessageBus>();
    }

    public void Configure(IServiceCollection services)
    {
        var configurator = new BusRegistrationConfigurator(services);
        configurator.Build();

        services.AddSingleton<IRabbitMqFactoryConfigurator>(this);
        services.AddSingleton<IPostBuildAction>(new PostBuildConfigureAction((context, cfg) =>
        {
            this.ConfigureEndpoints(context);
        }, this));

        services.AddSingleton<ConnectionProvider>(sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = ClientHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };
            return new ConnectionProvider(factory);
        });

        services.AddSingleton<ITransportFactory, RabbitMqTransportFactory>();
        services.AddSingleton<ISendContextFactory, RabbitMqSendContextFactory>();
        services.AddSingleton<IPublishContextFactory, RabbitMqPublishContextFactory>();
        services.AddSingleton<IMessageBus>([Throws(typeof(UriFormatException))] (sp) => new MessageBus(
            sp.GetRequiredService<ITransportFactory>(),
            sp,
            sp.GetRequiredService<ISendPipe>(),
            sp.GetRequiredService<IPublishPipe>(),
            sp.GetRequiredService<IMessageSerializer>(),
            new Uri($"rabbitmq://{ClientHost}/"),
            sp.GetRequiredService<ISendContextFactory>(),
            sp.GetRequiredService<IPublishContextFactory>()));

        services.AddSingleton<IReceiveEndpointConnector>(sp => (IReceiveEndpointConnector)sp.GetRequiredService<IMessageBus>());
        services.AddHostedService<ServiceBusHostedService>();
        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));
        services.AddScoped<IRequestClientFactory, RequestClientFactory>();
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
