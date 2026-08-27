using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public static class AzureServiceBusServiceCollectionExtensions
{
    public static IBusRegistrationConfigurator UsingAzureServiceBus(
        this IBusRegistrationConfigurator builder,
        Action<IBusRegistrationContext, IAzureServiceBusFactoryConfigurator> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var transportConfigurator = new AzureServiceBusFactoryConfigurator();
        RegisterTransport(builder.Services, transportConfigurator, configure);
        return builder;
    }

    internal static void RegisterTransport(
        IServiceCollection services,
        AzureServiceBusFactoryConfigurator configurator,
        Action<IBusRegistrationContext, IAzureServiceBusFactoryConfigurator> configure)
    {
        services.AddSingleton<IAzureServiceBusFactoryConfigurator>(configurator);
        services.AddSingleton<IPostBuildAction>(
            new AzureServiceBusPostBuildConfigureAction(configure, configurator));
        services.AddSingleton(_ => new ServiceBusClient(configurator.ConnectionString));
        services.AddSingleton<ITransportFactory, AzureServiceBusTransportFactory>();
        services.AddSingleton<IMessageBus>(provider => new MessageBus(
            provider.GetRequiredService<ITransportFactory>(),
            provider,
            provider.GetRequiredService<ISendPipe>(),
            provider.GetRequiredService<IPublishPipe>(),
            provider.GetRequiredService<MyServiceBus.Serialization.IMessageSerializer>(),
            AzureServiceBusTransportFactory.GetEndpoint(configurator.ConnectionString),
            provider.GetRequiredService<ISendContextFactory>(),
            provider.GetRequiredService<IPublishContextFactory>()));

        services.AddSingleton<IReceiveEndpointConnector>(provider =>
            (IReceiveEndpointConnector)provider.GetRequiredService<IMessageBus>());
        services.AddScoped(typeof(IRequestClient<>), typeof(GenericRequestClient<>));
        services.AddScoped<IRequestClientFactory, RequestClientFactory>();
    }
}

internal sealed class AzureServiceBusPostBuildConfigureAction : IPostBuildAction
{
    private readonly Action<IBusRegistrationContext, IAzureServiceBusFactoryConfigurator> _configure;
    private readonly AzureServiceBusFactoryConfigurator _configurator;

    public AzureServiceBusPostBuildConfigureAction(
        Action<IBusRegistrationContext, IAzureServiceBusFactoryConfigurator> configure,
        AzureServiceBusFactoryConfigurator configurator)
    {
        _configure = configure;
        _configurator = configurator;
    }

    public void Execute(IServiceProvider provider)
    {
        var context = new BusRegistrationContext(provider);
        _configure(context, _configurator);
        _configurator.Apply(provider.GetRequiredService<IMessageBus>(), provider);
    }
}
