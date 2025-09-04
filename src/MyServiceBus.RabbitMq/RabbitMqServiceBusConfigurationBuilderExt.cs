using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

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
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                //DispatchConsumersAsync = true
            };

            return new ConnectionProvider(factory);
        });

        builder.Services.AddSingleton<ITransportFactory, RabbitMqTransportFactory>();
        builder.Services.AddSingleton<IMessageBus>([Throws(typeof(InvalidOperationException), typeof(UriFormatException))] (sp) => new MessageBus(
            sp.GetRequiredService<ITransportFactory>(),
            sp,
            sp.GetRequiredService<ISendPipe>(),
            sp.GetRequiredService<IPublishPipe>(),
            sp.GetRequiredService<IMessageSerializer>(),
            new Uri($"rabbitmq://{rabbitConfigurator.ClientHost}/")));

        return builder;
    }
}

public sealed class ConnectionProvider
{
    private readonly IConnectionFactory connectionFactory;
    private IConnection? connection;
    private readonly SemaphoreSlim connectionLock = new(1, 1);

    public ConnectionProvider(IConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    [Throws(typeof(ObjectDisposedException), typeof(OperationCanceledException))]
    public async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (connection?.IsOpen == true)
            return connection;

        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (connection?.IsOpen == true)
                return connection;

            var delay = TimeSpan.FromMilliseconds(100);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var conn = await connectionFactory.CreateConnectionAsync(cancellationToken);
                    conn.ConnectionShutdownAsync += (_, _) =>
                    {
                        connection?.Dispose();
                        connection = null;
                        return Task.CompletedTask;
                    };

                    connection = conn;
                    return conn;
                }
                catch when (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                    if (delay > TimeSpan.FromSeconds(5))
                        delay = TimeSpan.FromSeconds(5);
                }
            }

            throw new OperationCanceledException("Cancelled while attempting to connect");
        }
        catch (ArgumentOutOfRangeException argumentOutOfRangeException)
        {
            throw;
        }
        finally
        {
            connectionLock.Release();
        }
    }
}
