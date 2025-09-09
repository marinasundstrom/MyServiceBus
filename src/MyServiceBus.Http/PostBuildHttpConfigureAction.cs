using Microsoft.Extensions.DependencyInjection;
using System;

namespace MyServiceBus;

internal class PostBuildHttpConfigureAction : IPostBuildAction
{
    private readonly Action<IBusRegistrationContext, IHttpFactoryConfigurator> _configure;
    private readonly IHttpFactoryConfigurator _configurator;

    public PostBuildHttpConfigureAction(Action<IBusRegistrationContext, IHttpFactoryConfigurator> configure, IHttpFactoryConfigurator configurator)
    {
        _configure = configure;
        _configurator = configurator;
    }

    [Throws(typeof(InvalidOperationException))]
    public void Execute(IServiceProvider provider)
    {
        var context = new HttpBusRegistrationContext(provider);
        _configure(context, _configurator);
        if (_configurator is HttpFactoryConfigurator cfg)
        {
            var bus = provider.GetRequiredService<IMessageBus>();
            cfg.Apply(bus, provider);
        }
    }
}

internal sealed class HttpBusRegistrationContext : IBusRegistrationContext
{
    public HttpBusRegistrationContext(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
}