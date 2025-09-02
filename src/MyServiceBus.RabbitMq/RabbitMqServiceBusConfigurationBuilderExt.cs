using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using RabbitMQ.Client;

namespace MyServiceBus;

public static class RabbitMqServiceBusConfigurationBuilderExt
{
    public static IBusRegistrationConfigurator UsingRabbitMq(
        this IBusRegistrationConfigurator builder,
        Action<IBusRegistrationContext, IRabbitMqFactoryConfigurator> configure)
    {
        var rabbitConfigurator = new RabbitMqFactoryConfigurator();

        // Save configuration for later, after the container is built
        builder.Services.AddSingleton<IRabbitMqFactoryConfigurator>(rabbitConfigurator);
        builder.Services.AddSingleton<IPostBuildAction>(new PostBuildConfigureAction(configure, rabbitConfigurator));

        // Register connection provider
        builder.Services.AddSingleton<ConnectionProvider>(sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = rabbitConfigurator.ClientHost,
                //DispatchConsumersAsync = true
            };

            return new ConnectionProvider(factory);
        });

        builder.Services.AddSingleton<ITransportFactory, RabbitMqTransportFactory>();
        builder.Services.AddSingleton<IMessageBus>(sp => new MyMessageBus(
            sp.GetRequiredService<ITransportFactory>(),
            sp,
            sp.GetRequiredService<ISendPipe>(),
            sp.GetRequiredService<IPublishPipe>(),
            sp.GetRequiredService<IMessageSerializer>()));

        return builder;
    }
}

public sealed class ConnectionProvider
{
    private readonly IConnectionFactory connectionFactory;
    private IConnection connection;

    public ConnectionProvider(IConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default)
        => connection ??= await connectionFactory.CreateConnectionAsync(cancellationToken);
}
