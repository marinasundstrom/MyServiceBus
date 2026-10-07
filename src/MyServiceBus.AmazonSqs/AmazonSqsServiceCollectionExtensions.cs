using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class AmazonSqsServiceCollectionExtensions
{
    public static IBusRegistrationConfigurator UsingAmazonSqs(
        this IBusRegistrationConfigurator builder,
        Action<IBusRegistrationContext, IAmazonSqsFactoryConfigurator> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var transportConfigurator = new AmazonSqsFactoryConfigurator();
        RegisterTransport(builder.Services, transportConfigurator, configure);
        return builder;
    }

    internal static void RegisterTransport(
        IServiceCollection services,
        AmazonSqsFactoryConfigurator configurator,
        Action<IBusRegistrationContext, IAmazonSqsFactoryConfigurator> configure)
    {
        services.AddSingleton<IAmazonSqsFactoryConfigurator>(configurator);
        var action = new AmazonSqsPostBuildConfigureAction(configure, configurator);
        services.AddSingleton<IPostBuildAction>(action);
        services.AddSingleton<IAmazonSQS>(provider =>
        {
            action.Configure(provider);
            return CreateSqsClient(configurator);
        });
        services.AddSingleton<IAmazonSimpleNotificationService>(provider =>
        {
            action.Configure(provider);
            return CreateSnsClient(configurator);
        });
        services.AddSingleton<ITransportFactory, AmazonSqsTransportFactory>();
        services.AddSingleton<IMessageBus>(provider => new MessageBus(
            provider.GetRequiredService<ITransportFactory>(), provider,
            provider.GetRequiredService<ISendPipe>(), provider.GetRequiredService<IPublishPipe>(),
            provider.GetRequiredService<MyServiceBus.Serialization.IMessageSerializer>(),
            new Uri($"amazonsqs://{configurator.Region}/"),
            provider.GetRequiredService<ISendContextFactory>(),
            provider.GetRequiredService<IPublishContextFactory>()));
        services.AddSingleton<IReceiveEndpointConnector>(provider =>
            (IReceiveEndpointConnector)provider.GetRequiredService<IMessageBus>());
        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));
        services.AddScoped<IRequestClientFactory, RequestClientFactory>();
    }

    private static IAmazonSQS CreateSqsClient(AmazonSqsFactoryConfigurator configurator)
    {
        var config = new AmazonSQSConfig();
        if (configurator.ServiceUrl is not null)
        {
            config.ServiceURL = configurator.ServiceUrl;
            config.AuthenticationRegion = configurator.Region;
            return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), config);
        }
        config.RegionEndpoint = RegionEndpoint.GetBySystemName(configurator.Region);
        return new AmazonSQSClient(config);
    }

    private static IAmazonSimpleNotificationService CreateSnsClient(AmazonSqsFactoryConfigurator configurator)
    {
        var config = new AmazonSimpleNotificationServiceConfig();
        if (configurator.ServiceUrl is not null)
        {
            config.ServiceURL = configurator.ServiceUrl;
            config.AuthenticationRegion = configurator.Region;
            return new AmazonSimpleNotificationServiceClient(new BasicAWSCredentials("test", "test"), config);
        }
        config.RegionEndpoint = RegionEndpoint.GetBySystemName(configurator.Region);
        return new AmazonSimpleNotificationServiceClient(config);
    }
}

internal sealed class AmazonSqsPostBuildConfigureAction : IPostBuildAction
{
    private readonly Action<IBusRegistrationContext, IAmazonSqsFactoryConfigurator> _configure;
    private readonly AmazonSqsFactoryConfigurator _configurator;

    public AmazonSqsPostBuildConfigureAction(
        Action<IBusRegistrationContext, IAmazonSqsFactoryConfigurator> configure,
        AmazonSqsFactoryConfigurator configurator)
    {
        _configure = configure;
        _configurator = configurator;
    }

    private readonly object configurationLock = new();
    private bool configured;
    private Exception? configurationFailure;

    internal void Configure(IServiceProvider provider)
    {
        lock (configurationLock)
        {
            if (configured) return;
            if (configurationFailure is not null)
                throw new InvalidOperationException("Transport configuration previously failed. Rebuild the service provider before retrying.", configurationFailure);
            try
            {
                _configure(new BusRegistrationContext(provider), _configurator);
                configured = true;
            }
            catch (Exception exception)
            {
                configurationFailure = exception;
                throw;
            }
        }
    }

    public void Execute(IServiceProvider provider)
    {
        Configure(provider);
        _configurator.Apply(provider.GetRequiredService<IMessageBus>(), provider);
    }
}
