using System;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using System.Reflection;

namespace MyServiceBus;

public interface IRegistrationConfigurator
//: IServiceCollection
{
    void AddConsumer<T>() where T : class, IConsumer;

    void AddConsumers(params Assembly[] assemblies);

    void AddConsumer<TConsumer, TMessage>(Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    void ConfigureSend(Action<PipeConfigurator<SendContext>> configure);

    void ConfigurePublish(Action<PipeConfigurator<PublishContext>> configure);

    void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer;

    /*
    IConsumerRegistrationConfigurator<T> AddConsumer<T>(Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
            where T : class, IConsumer;

IConsumerRegistrationConfigurator<T> AddConsumer<T>(Type consumerDefinitionType,
    Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
    where T : class, IConsumer;
    */
}
