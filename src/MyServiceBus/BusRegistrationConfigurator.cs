using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;
using System;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyServiceBus;

public class BusRegistrationConfigurator : IBusRegistrationConfigurator
{
    private TopologyRegistry _topology = new TopologyRegistry();
    private readonly PipeConfigurator<SendContext> sendConfigurator = new();
    private readonly PipeConfigurator<PublishContext> publishConfigurator = new();
    private Type serializerType = typeof(EnvelopeMessageSerializer);
    private readonly TransportCapabilityRequirements capabilityRequirements = new();

    public IServiceCollection Services { get; }

    public BusRegistrationConfigurator(IServiceCollection services)
    {
        Services = services;
        sendConfigurator.UseFilter<OpenTelemetrySendFilter>();
        publishConfigurator.UseFilter<OpenTelemetrySendFilter>();
    }

    public void AddConsumer<TConsumer>() where TConsumer : class, IConsumer
    {
        var messageTypes = GetHandledMessageTypes(typeof(TConsumer));
        if (messageTypes.Length == 0)
            throw new InvalidOperationException($"Consumer type {typeof(TConsumer)} does not implement IConsumer<TMessage>. Use AddConsumerMethods<TConsumer>() for method consumers.");

        Services.AddScoped<TConsumer>();
        Services.AddScoped<IConsumer, TConsumer>((sp) => sp.GetRequiredService<TConsumer>());

        var messageType = messageTypes.First();
        var attributeEndpointName = typeof(TConsumer).GetCustomAttribute<ConsumerAttribute>()?.EndpointName;
        var endpointName = attributeEndpointName
            ?? DefaultEndpointNameFormatter.Instance.Format(typeof(TConsumer));

        _topology.RegisterConsumerWithEndpointMetadata<TConsumer>(
          queueName: endpointName,
          configurePipe: null,
          endpointNameIsExplicit: attributeEndpointName is not null,
          endpointNameFormatterType: typeof(TConsumer),
          messageTypes: messageType
      );
    }

    public void AddConsumerMethods<TConsumer>(string? endpointName = null) where TConsumer : class
        => AddConsumerMethods(typeof(TConsumer), endpointName);

    public void AddConsumerMethods(Type declaringType, string? endpointName = null)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        if (endpointName is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        var definitions = ReflectionConsumerMethodDiscovery.Discover(declaringType).ToArray();
        if (definitions.Length == 0)
            throw new InvalidOperationException($"Consumer method type {declaringType} does not declare any eligible methods.");
        if (definitions.Any(definition => !definition.Method.IsStatic))
            Services.AddScoped(declaringType);
        foreach (var definition in definitions)
            _topology.RegisterConsumerMethod(definition, endpointName);
    }

    public void AddConsumer<TConsumer, TMessage>(Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        Services.AddScoped<TConsumer>();
        Services.AddScoped<IConsumer, TConsumer>(sp => sp.GetRequiredService<TConsumer>());
        RegisterConsumerUsingDeclaredEndpoint<TConsumer, TMessage>(configure);
    }

    public void AddConsumer<TConsumer, TMessage>(
        Func<IServiceProvider, TConsumer> consumerFactory,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(consumerFactory);
        Services.AddScoped(consumerFactory);
        Services.AddScoped<IConsumer, TConsumer>(sp => sp.GetRequiredService<TConsumer>());
        RegisterConsumerUsingDeclaredEndpoint<TConsumer, TMessage>(configure);
    }

    public void AddConsumer<TConsumer, TMessage>(
        string endpointName,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        Services.AddScoped<TConsumer>();
        Services.AddScoped<IConsumer, TConsumer>((sp) => sp.GetRequiredService<TConsumer>());

        _topology.RegisterConsumer<TConsumer, TMessage>(
            queueName: endpointName,
            configurePipe: configure,
            endpointNameIsExplicit: true);
    }

    public void AddConsumer<TConsumer, TMessage>(
        string endpointName,
        Func<IServiceProvider, TConsumer> consumerFactory,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(consumerFactory);
        Services.AddScoped(consumerFactory);
        Services.AddScoped<IConsumer, TConsumer>(sp => sp.GetRequiredService<TConsumer>());

        _topology.RegisterConsumer<TConsumer, TMessage>(
            queueName: endpointName,
            configurePipe: configure,
            endpointNameIsExplicit: true);
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void AddGeneratedConsumer<TConsumer, TMessage>(
        string endpointName,
        Type? endpointNameFormatterType,
        bool endpointNameIsExplicit,
        Func<IServiceProvider, TConsumer> consumerFactory)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(consumerFactory);

        Services.AddScoped(consumerFactory);
        Services.AddScoped<IConsumer, TConsumer>(sp => sp.GetRequiredService<TConsumer>());
        _topology.RegisterConsumerWithEndpointMetadata<TConsumer, TMessage>(
            endpointName,
            configurePipe: null,
            endpointNameIsExplicit: endpointNameIsExplicit,
            endpointNameFormatterType: endpointNameFormatterType);
    }

    private void RegisterConsumerUsingDeclaredEndpoint<TConsumer, TMessage>(
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        var attributeEndpointName = typeof(TConsumer).GetCustomAttribute<ConsumerAttribute>()?.EndpointName;
        _topology.RegisterConsumer<TConsumer, TMessage>(
            attributeEndpointName ?? DefaultEndpointNameFormatter.Instance.Format(typeof(TConsumer)),
            configure,
            endpointNameIsExplicit: attributeEndpointName is not null,
            endpointNameFormatterType: typeof(TConsumer));
    }

    public void AddConsumers(params Assembly[] assemblies)
    {
        AddConsumers(static _ => true, assemblies);
    }

    public void AddConsumers(Func<Type, bool> typeFilter, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(typeFilter);
        ArgumentNullException.ThrowIfNull(assemblies);

        var consumerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(typeFilter)
            .Where(t => typeof(IConsumer).IsAssignableFrom(t)
                        && t.IsClass
                        && !t.IsAbstract
                        && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false)
                        && GetHandledMessageTypes(t).Length > 0
                        && !t.ContainsGenericParameters);

        var method = GetType().GetMethods()
            .First(m => m.Name == nameof(AddConsumer)
                        && m.GetGenericArguments().Length == 1
                        && m.GetParameters().Length == 0);

        foreach (var type in consumerTypes)
        {
            var generic = method.MakeGenericMethod(type);
            generic.Invoke(this, null);
        }

        foreach (var definition in assemblies.SelectMany(assembly =>
            ReflectionConsumerMethodDiscovery.Discover(assembly, typeFilter)))
        {
            if (!definition.Method.IsStatic)
                Services.AddScoped(definition.Method.DeclaringType!);
            _topology.RegisterConsumerMethod(definition);
        }
    }

    public void ConfigureSend(Action<PipeConfigurator<SendContext>> configure)
    {
        configure(sendConfigurator);
    }

    public void ConfigurePublish(Action<PipeConfigurator<PublishContext>> configure)
    {
        configure(publishConfigurator);
    }

    public void AddHook<THook>() where THook : class, IBusHook
    {
        Services.AddSingleton<IBusHook, THook>();
    }

    public void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer
    {
        serializerType = typeof(TSerializer);
    }

    public void RequireTransportCapability(string capability, bool requireNative = false)
    {
        capabilityRequirements.Require(capability, requireNative);
    }

    private static Type[] GetHandledMessageTypes(Type consumerType)
    {
        return consumerType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToArray();
    }

    public void Build()
    {
        if (!Services.Any(d => d.ServiceType == typeof(ILoggerFactory)))
            Services.AddLogging(b => b.AddSimpleConsole());

        Services.AddSingleton(_topology);
        Services.AddSingleton<IBusTopology>(_ => _topology);
        Services.AddSingleton<IBusHookDispatcher, BusHookDispatcher>();
        Services.AddSingleton<IRetryObserver, BusHookRetryObserver>();
        Services.AddSingleton<IPostBuildAction>(_ => new ConsumerRegistrationAction(_topology));
        Services.AddSingleton<ISendPipe>((sp) => new SendPipe(sendConfigurator.Build(sp)));
        Services.AddSingleton<IPublishPipe>((sp) => new PublishPipe(publishConfigurator.Build(sp)));
        Services.AddSingleton(typeof(IMessageSerializer), serializerType);
        Services.AddSingleton(capabilityRequirements);
        Services.AddSingleton<ISendContextFactory, SendContextFactory>();
        Services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        Services.AddScoped<ConsumeContextProvider>();
        Services.AddScoped<ISendEndpointProvider, SendEndpointProvider>();
        Services.AddScoped<IPublishEndpointProvider, PublishEndpointProvider>();
        Services.AddScoped<IPublishEndpoint>((sp) => sp.GetRequiredService<IPublishEndpointProvider>().GetPublishEndpoint());
    }
}
