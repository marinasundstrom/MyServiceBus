using System;

namespace MyServiceBus;

public static class BusFactoryExtensions
{
    public static IMessageBus CreateUsingRabbitMq(this BusFactory factory, Action<IRabbitMqFactoryConfigurator>? configure = null)
    {
        return factory.Create(services =>
            RabbitMqBusFactory.Configure(services, null, (_, cfg) => configure?.Invoke(cfg)));
    }
}
