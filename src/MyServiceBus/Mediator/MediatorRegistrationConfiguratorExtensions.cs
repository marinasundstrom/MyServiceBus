using System.Diagnostics.CodeAnalysis;

namespace MyServiceBus;

public static class MediatorRegistrationConfiguratorExtensions
{
    [RequiresDynamicCode("Runtime handler discovery closes generic registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime handler discovery cannot guarantee that handler metadata is preserved. Use AddGeneratedConsumers for trimmed applications.")]
    public static void AddHandler<THandler>(this IRegistrationConfigurator configurator)
        where THandler : class, IHandler
        => configurator.AddConsumer<THandler>();

    public static void AddHandler<THandler, TMessage>(
        this IRegistrationConfigurator configurator,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where THandler : class, IHandler<TMessage>
        where TMessage : class
        => configurator.AddConsumer<THandler, TMessage>(configure);

    public static void AddHandler<THandler, TMessage, TResponse>(
        this IRegistrationConfigurator configurator,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where THandler : class, IHandler<TMessage, TResponse>
        where TMessage : class
        where TResponse : class
        => configurator.AddConsumer<THandler, TMessage>(configure);
}
