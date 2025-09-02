using System;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public interface IRegistrationConfigurator
//: IServiceCollection
{
    void AddConsumer<T>() where T : class, IConsumer;

    void AddConsumer<TConsumer, TMessage>(Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    /*
    IConsumerRegistrationConfigurator<T> AddConsumer<T>(Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
            where T : class, IConsumer;

IConsumerRegistrationConfigurator<T> AddConsumer<T>(Type consumerDefinitionType,
    Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
    where T : class, IConsumer;
    */
}
