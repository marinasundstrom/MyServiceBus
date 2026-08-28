using System;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using System.Reflection;

namespace MyServiceBus;

public interface IRegistrationConfigurator
//: IServiceCollection
{
    void AddConsumer<T>() where T : class, IConsumer;

    void AddConsumerMethods<TConsumer>(string? endpointName = null) where TConsumer : class;

    void AddConsumerMethods(Type declaringType, string? endpointName = null);

    void AddConsumers(params Assembly[] assemblies);

    void AddConsumers(Func<Type, bool> typeFilter, params Assembly[] assemblies);

    void AddConsumer<TConsumer, TMessage>(Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    void AddConsumer<TConsumer, TMessage>(
        Func<IServiceProvider, TConsumer> consumerFactory,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    void AddConsumer<TConsumer, TMessage>(
        string endpointName,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    void AddConsumer<TConsumer, TMessage>(
        string endpointName,
        Func<IServiceProvider, TConsumer> consumerFactory,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    void AddGeneratedConsumer<TConsumer, TMessage>(
        string endpointName,
        Type? endpointNameFormatterType,
        bool endpointNameIsExplicit,
        Func<IServiceProvider, TConsumer> consumerFactory)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    void ConfigureSend(Action<PipeConfigurator<SendContext>> configure);

    void ConfigurePublish(Action<PipeConfigurator<PublishContext>> configure);

    void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer;

    void RequireTransportCapability(string capability, bool requireNative = false);

    /*
    IConsumerRegistrationConfigurator<T> AddConsumer<T>(Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
            where T : class, IConsumer;

IConsumerRegistrationConfigurator<T> AddConsumer<T>(Type consumerDefinitionType,
    Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
    where T : class, IConsumer;
    */
}
