namespace MyServiceBus;

using System;

public interface IBusFactory
{
    IMessageBus Create<T>(Action<T>? configure = null)
        where T : IBusFactoryConfigurator, new();
}
