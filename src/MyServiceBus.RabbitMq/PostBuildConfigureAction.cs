using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

internal class PostBuildConfigureAction : IPostBuildAction
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

    [Throws(typeof(InvalidOperationException))]
    public void Execute(IServiceProvider provider)
    {
        var context = new BusRegistrationContext(provider);
        _configure(context, _configurator);
        if (_configurator is RabbitMqFactoryConfigurator cfg)
        {
            var bus = provider.GetRequiredService<IMessageBus>();
            cfg.Apply(bus);
        }
    }
}