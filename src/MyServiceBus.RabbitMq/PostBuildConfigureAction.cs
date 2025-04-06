namespace MyServiceBus;

public class PostBuildConfigureAction : IPostBuildAction
{
    private readonly Action<IBusRegistrationContext, IRabbitMqFactoryConfigurator> _configure;
    private readonly IRabbitMqFactoryConfigurator _configurator;

    public PostBuildConfigureAction(
        Action<IBusRegistrationContext, IRabbitMqFactoryConfigurator> configure,
        IRabbitMqFactoryConfigurator configurator)
    {
        _configure = configure;
        _configurator = configurator;
    }

    public void Execute(IServiceProvider provider)
    {
        var context = new BusRegistrationContext(provider);
        _configure(context, _configurator);
    }
}