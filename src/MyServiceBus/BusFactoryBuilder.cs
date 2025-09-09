using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public class BusFactoryBuilder : IBusFactory
{
    readonly IBusFactory inner;
    ILoggerFactory? loggerFactory;
    readonly IList<Action<IServiceCollection>> serviceActions = new List<Action<IServiceCollection>>();

    public BusFactoryBuilder(IBusFactory inner)
    {
        this.inner = inner;
    }

    public BusFactoryBuilder WithLoggerFactory(ILoggerFactory factory)
    {
        loggerFactory = factory;
        return this;
    }

    public BusFactoryBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        serviceActions.Add(configure);
        return this;
    }

    public IMessageBus Create<T>(Action<T>? configure = null) where T : IBusFactoryConfigurator, new()
    {
        var cfg = new T();
        configure?.Invoke(cfg);

        var services = new ServiceCollection();
        foreach (var action in serviceActions)
            action(services);
        if (loggerFactory != null)
            services.AddSingleton(loggerFactory);

        cfg.Configure(services);
        var provider = services.BuildServiceProvider();

        foreach (var action in provider.GetServices<IPostBuildAction>())
            action.Execute(provider);

        return provider.GetRequiredService<IMessageBus>();
    }
}

