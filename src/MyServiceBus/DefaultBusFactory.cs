namespace MyServiceBus;

using System;

public class DefaultBusFactory : IBusFactory
{
    public IMessageBus Create<T>(Action<T>? configure = null)
        where T : IBusFactoryConfigurator, new()
    {
        var cfg = new T();
        configure?.Invoke(cfg);
        return cfg.Build();
    }
}
