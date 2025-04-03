namespace MyServiceBus;

public static class RabbitMqServiceBusConfigurationBuilderExt
{
    public static IBusRegistrationConfigurator UsingRabbitMq(this IBusRegistrationConfigurator builder, Action<IBusRegistrationContext, IRabbiqMqFactoryConfigurator> c)
    {
        var busRegistrationContext = new BusRegistrationContext();

        //builder.Services.AddSingleton<IBus>();
        //builder.Services.AddSingleton<IBusControl>();

        //builder.Services.AddSingleton<IReceiveEndpointConnector>();
        //builder.Services.AddScoped<ISendEndpointProvider>();
        //builder.Services.AddScoped<IPublishEndpoint>();
        //builder.Services.AddScoped(typeof(IRequestClient<>));

        var configuration = new RabbiqMqFactoryConfigurator();
        c?.Invoke(busRegistrationContext, configuration);

        return builder;
    }
}
