using System;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace MyServiceBus;

public interface IRegistrationConfigurator
//: IServiceCollection
{
    [RequiresDynamicCode("Runtime consumer discovery closes generic registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer discovery cannot guarantee that consumer metadata is preserved. Use AddGeneratedConsumers for trimmed applications.")]
    void AddConsumer<T>() where T : class, IConsumer;

    [RequiresDynamicCode("Runtime consumer method discovery closes generic registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer method discovery requires method and parameter metadata. Use AddGeneratedConsumers for trimmed applications.")]
    void AddConsumerMethods<TConsumer>(string? endpointName = null) where TConsumer : class;

    [RequiresDynamicCode("Runtime consumer method discovery closes generic registration descriptors dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer method discovery requires method and parameter metadata. Use AddGeneratedConsumers for trimmed applications.")]
    void AddConsumerMethods(Type declaringType, string? endpointName = null);

    [RequiresDynamicCode("Assembly scanning closes generic consumer registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Assembly scanning cannot guarantee that consumer metadata is preserved. Use AddGeneratedConsumers for trimmed applications.")]
    void AddConsumers(params Assembly[] assemblies);

    [RequiresDynamicCode("Assembly scanning closes generic consumer registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Assembly scanning cannot guarantee that consumer metadata is preserved. Use AddGeneratedConsumers for trimmed applications.")]
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

    void AddSerializer(ISerializerFactory factory, bool isSerializer = false);

    void AddDeserializer(ISerializerFactory factory, bool isDefault = false);

    void ClearSerialization();

    void RequireTransportCapability(string capability, bool requireNative = false);

    /*
    IConsumerRegistrationConfigurator<T> AddConsumer<T>(Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
            where T : class, IConsumer;

IConsumerRegistrationConfigurator<T> AddConsumer<T>(Type consumerDefinitionType,
    Action<IRegistrationContext, IConsumerConfigurator<T>> configure = null)
    where T : class, IConsumer;
    */
}
