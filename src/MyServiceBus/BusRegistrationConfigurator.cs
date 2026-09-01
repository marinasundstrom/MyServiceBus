using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyServiceBus.Choreography;
using MyServiceBus.Orchestration;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;
using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyServiceBus;

public class BusRegistrationConfigurator : IBusRegistrationConfigurator
{
    private TopologyRegistry _topology = new TopologyRegistry();
    private readonly PipeConfigurator<SendContext> sendConfigurator = new();
    private readonly PipeConfigurator<PublishContext> publishConfigurator = new();
    private ISerializerFactory? serializerFactory = new EnvelopeSerializerFactory();
    private readonly List<ISerializerFactory> deserializerFactories =
    [
        new EnvelopeSerializerFactory(),
        new RawJsonSerializerFactory(),
        new NServiceBusJsonSerializerFactory()
    ];
    private string defaultContentType = InboundMessageResolver.EnvelopeContentType;
    private readonly TransportCapabilityRequirements capabilityRequirements = new();
    private readonly JobConsumerRegistry jobConsumers = new();
    private readonly HashSet<Type> sagaStateMachines = new();

    public IServiceCollection Services { get; }

    public BusRegistrationConfigurator(IServiceCollection services)
    {
        Services = services;
        var telemetryFilter = new OpenTelemetrySendFilter();
        sendConfigurator.UseFilter((IFilter<SendContext>)telemetryFilter);
        publishConfigurator.UseFilter((IFilter<PublishContext>)telemetryFilter);
    }

    public void AddChoreography(ChoreographyFragment fragment)
    {
        _topology.RegisterChoreography(fragment);
    }

    public void AddSagaStateMachine<TStateMachine, TSaga>(
        TStateMachine stateMachine,
        string? endpointName = null)
        where TStateMachine : SagaStateMachine<TSaga>
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(stateMachine);
        if (sagaStateMachines.Contains(typeof(TStateMachine)))
            return;

        var repository = stateMachine.CreateConfiguredInMemoryRepository();
        if (RegisterSagaStateMachine(
            stateMachine,
            repository.Capabilities,
            _ => repository,
            endpointName))
        {
            Services.AddSingleton(repository);
            Services.AddSingleton<ISagaRepository<TSaga>>(repository);
        }
    }

    public void AddSagaStateMachine<TStateMachine, TSaga>(
        TStateMachine stateMachine,
        ISagaRepository<TSaga> repository,
        string? endpointName = null)
        where TStateMachine : SagaStateMachine<TSaga>
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(stateMachine);
        ArgumentNullException.ThrowIfNull(repository);
        if (RegisterSagaStateMachine(
            stateMachine,
            repository.Capabilities,
            _ => repository,
            endpointName))
        {
            Services.AddSingleton<ISagaRepository<TSaga>>(repository);
        }
    }

    public void AddSagaStateMachine<TStateMachine, TSaga>(
        TStateMachine stateMachine,
        SagaRepositoryCapabilities capabilities,
        Func<IServiceProvider, ISagaRepository<TSaga>> repositoryFactory,
        string? endpointName = null)
        where TStateMachine : SagaStateMachine<TSaga>
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(stateMachine);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(repositoryFactory);
        RegisterSagaStateMachine(stateMachine, capabilities, repositoryFactory, endpointName);
    }

    private bool RegisterSagaStateMachine<TStateMachine, TSaga>(
        TStateMachine stateMachine,
        SagaRepositoryCapabilities capabilities,
        Func<IServiceProvider, ISagaRepository<TSaga>> repositoryFactory,
        string? endpointName)
        where TStateMachine : SagaStateMachine<TSaga>
        where TSaga : class
    {
        if (endpointName is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        if (!sagaStateMachines.Add(typeof(TStateMachine)))
            return false;

        capabilities.EnsureSupports(
            stateMachine.Definition.RepositoryRequirements,
            stateMachine.Definition.CompletionPolicy);
        var queueName = endpointName ?? stateMachine.Definition.StateMachineId;

        _topology.RegisterSagaStateMachine(stateMachine.Definition, queueName);

        Services.AddSingleton(stateMachine);
        stateMachine.RegisterConsumers<TStateMachine>(
            this,
            serviceProvider => stateMachine.CreateRuntime(repositoryFactory(serviceProvider)),
            queueName);
        return true;
    }

    public void AddSagaStateMachine<TStateMachine, TSaga>(string? endpointName = null)
        where TStateMachine : SagaStateMachine<TSaga>, new()
        where TSaga : class
        => AddSagaStateMachine<TStateMachine, TSaga>(new TStateMachine(), endpointName);

    [RequiresDynamicCode("Runtime job consumer discovery closes generic registrations dynamically. Use the explicit job/message overload for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime job consumer discovery cannot guarantee that generic interface metadata is preserved.")]
    public void AddJobConsumer<TConsumer>(Action<JobConsumerOptions>? configure = null)
        where TConsumer : class, IJobConsumer
    {
        var interfaces = typeof(TConsumer).GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IJobConsumer<>))
            .ToArray();
        if (interfaces.Length != 1)
            throw new InvalidOperationException($"Job consumer type {typeof(TConsumer)} must implement exactly one IJobConsumer<TJob> interface.");

        var method = GetType().GetMethods()
            .Single(candidate => candidate.Name == nameof(AddJobConsumer)
                && candidate.IsGenericMethodDefinition
                && candidate.GetGenericArguments().Length == 2);
        method.MakeGenericMethod(typeof(TConsumer), interfaces[0].GetGenericArguments()[0])
            .Invoke(this, [configure]);
    }

    public void AddJobConsumer<TConsumer, TJob>(Action<JobConsumerOptions>? configure = null)
        where TConsumer : class, IJobConsumer<TJob>
        where TJob : class
    {
        var options = new JobConsumerOptions();
        configure?.Invoke(options);
        Services.AddScoped<TConsumer>();
        jobConsumers.Add<TConsumer, TJob>(options);
    }

    [RequiresDynamicCode("Runtime consumer discovery closes generic registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer discovery cannot guarantee that consumer metadata is preserved. Use AddGeneratedConsumers for trimmed applications.")]
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

    [RequiresDynamicCode("Runtime consumer method discovery closes generic registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer method discovery requires method and parameter metadata. Use AddGeneratedConsumers for trimmed applications.")]
    public void AddConsumerMethods<TConsumer>(string? endpointName = null) where TConsumer : class
        => AddConsumerMethods(typeof(TConsumer), endpointName);

    [RequiresDynamicCode("Runtime consumer method discovery closes generic registration descriptors dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer method discovery requires method and parameter metadata. Use AddGeneratedConsumers for trimmed applications.")]
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

    [RequiresDynamicCode("Assembly scanning closes generic consumer registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Assembly scanning cannot guarantee that consumer metadata is preserved. Use AddGeneratedConsumers for trimmed applications.")]
    public void AddConsumers(params Assembly[] assemblies)
    {
        AddConsumers(static _ => true, assemblies);
    }

    [RequiresDynamicCode("Assembly scanning closes generic consumer registrations dynamically. Use AddGeneratedConsumers for NativeAOT.")]
    [RequiresUnreferencedCode("Assembly scanning cannot guarantee that consumer metadata is preserved. Use AddGeneratedConsumers for trimmed applications.")]
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

    public void AddSerializer(ISerializerFactory factory, bool isSerializer = false)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (isSerializer)
            serializerFactory = factory;
    }

    public void AddDeserializer(ISerializerFactory factory, bool isDefault = false)
    {
        ArgumentNullException.ThrowIfNull(factory);
        deserializerFactories.Add(factory);
        if (isDefault)
            defaultContentType = factory.ContentType;
    }

    public void ClearSerialization()
    {
        serializerFactory = null;
        deserializerFactories.Clear();
        defaultContentType = string.Empty;
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
        Services.AddSingleton(jobConsumers);
        Services.AddSingleton<IJobConsumerRegistry>(provider => provider.GetRequiredService<JobConsumerRegistry>());
        Services.AddSingleton<IBusTopology>(_ => _topology);
        Services.AddSingleton<IBusHookDispatcher, BusHookDispatcher>();
        Services.AddSingleton<IRetryObserver, BusHookRetryObserver>();
        Services.AddSingleton<IPostBuildAction>(_ => new ConsumerRegistrationAction(_topology));
        Services.AddSingleton<ISendPipe>((sp) => new SendPipe(sendConfigurator.Build(sp)));
        Services.AddSingleton<IPublishPipe>((sp) => new PublishPipe(publishConfigurator.Build(sp)));
        var configuredSerializerFactory = serializerFactory
            ?? throw new InvalidOperationException("No message serializer is configured.");
        if (deserializerFactories.Count == 0)
            throw new InvalidOperationException("No message deserializers are configured.");

        Services.AddSingleton(_ => configuredSerializerFactory.CreateSerializer());
        Services.AddSingleton<IInboundMessageResolver>(_ => new InboundMessageResolver(
            deserializerFactories.Select(factory => factory.CreateDeserializer()),
            defaultContentType));
        Services.AddSingleton(capabilityRequirements);
        Services.AddSingleton<ISendContextFactory, SendContextFactory>();
        Services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        Services.AddScoped<ConsumeContextProvider>();
        Services.AddScoped<ISendEndpointProvider, SendEndpointProvider>();
        Services.AddScoped<IPublishEndpointProvider, PublishEndpointProvider>();
        Services.AddScoped<IPublishEndpoint>((sp) => sp.GetRequiredService<IPublishEndpointProvider>().GetPublishEndpoint());
    }
}
