using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using MyServiceBus.Choreography;
using MyServiceBus.Orchestration;

namespace MyServiceBus.Topology;

public class TopologyRegistry : IBusTopology
{
    public List<MessageTopology> Messages { get; } = new();
    public List<ConsumerTopology> Consumers { get; } = new();
    private readonly List<ConsumerDefinitionModel> consumerDefinitions = new();
    private readonly List<ReceiveEndpointDefinition> _receiveEndpoints = new();
    private readonly List<ChoreographyFragment> choreographies = new();
    private readonly List<SagaStateMachineTopology> sagaStateMachines = new();

    public IReadOnlyList<ReceiveEndpointDefinition> ReceiveEndpoints => _receiveEndpoints;
    public IReadOnlyList<ConsumerDefinitionModel> ConsumerDefinitions => consumerDefinitions.AsReadOnly();
    public IReadOnlyList<ChoreographyFragment> Choreographies => choreographies;
    public IReadOnlyList<SagaStateMachineTopology> SagaStateMachines => sagaStateMachines;

    public void RegisterSagaStateMachine(SagaStateMachineDefinition definition, string endpointName)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        definition.Validate();

        if (sagaStateMachines.Any(existing =>
            string.Equals(existing.Definition.StateMachineId, definition.StateMachineId, StringComparison.Ordinal) &&
            string.Equals(existing.Definition.Owner, definition.Owner, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Saga state machine '{definition.StateMachineId}' is already registered by '{definition.Owner}'.",
                nameof(definition));
        }

        sagaStateMachines.Add(new SagaStateMachineTopology(definition, endpointName));
    }

    public void RegisterChoreography(ChoreographyFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        fragment.Validate();

        if (choreographies.Any(existing =>
            string.Equals(existing.ChoreographyId, fragment.ChoreographyId, StringComparison.Ordinal) &&
            string.Equals(existing.Owner, fragment.Owner, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Choreography '{fragment.ChoreographyId}' already has a fragment owned by '{fragment.Owner}'.",
                nameof(fragment));
        }

        choreographies.Add(fragment);
    }

    public void RegisterMessage<T>(string entityName)
    {
        Messages.Add(new MessageTopology
        {
            MessageType = typeof(T),
            EntityName = entityName
        });
    }

    private MessageTopology RegisterMessage(Type messageType, string? entityName = null)
    {
        var messageTopology = new MessageTopology
        {
            MessageType = messageType,
            EntityName = entityName ?? EntityNameFormatter.Format(messageType)
        };
        Messages.Add(messageTopology);
        return messageTopology;
    }

    [RequiresDynamicCode("Closing a consumer registration descriptor at runtime requires dynamic generic code. Use the typed RegisterConsumer overload for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer discovery cannot guarantee that consumer metadata is preserved. Use the typed RegisterConsumer overload for trimmed applications.")]
    public void RegisterConsumer<TConsumer>(string queueName, Delegate? configurePipe, params Type[] messageTypes)
        => RegisterConsumerWithEndpointMetadata<TConsumer>(
            queueName,
            configurePipe,
            endpointNameIsExplicit: false,
            endpointNameFormatterType: typeof(TConsumer),
            definition: null,
            messageTypes: messageTypes);

    internal ConsumerDefinitionModel RegisterConsumerWithEndpointMetadata<TConsumer>(
        string queueName,
        Delegate? configurePipe,
        bool endpointNameIsExplicit,
        Type? endpointNameFormatterType,
        IConsumerDefinition? definition = null,
        params Type[] messageTypes)
    {
        var model = CreateConsumerDefinition(
            typeof(TConsumer),
            queueName,
            endpointNameIsExplicit,
            endpointNameFormatterType,
            definition,
            messageTypes);

        foreach (var messageType in model.MessageTypes)
        {
            MaterializeConsumer(
                model,
                configurePipe,
                ReflectionConsumerRegistrationDescriptorFactory.Create(typeof(TConsumer), messageType),
                [messageType]);
        }

        return model;
    }

    public void RegisterConsumer<TConsumer, TMessage>(
        string queueName,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configurePipe = null,
        bool endpointNameIsExplicit = false,
        Type? endpointNameFormatterType = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        RegisterConsumerWithEndpointMetadata<TConsumer, TMessage>(
            queueName,
            configurePipe,
            endpointNameIsExplicit,
            endpointNameFormatterType ?? typeof(TConsumer));
    }

    internal ConsumerDefinitionModel RegisterConsumerWithEndpointMetadata<TConsumer, TMessage>(
        string queueName,
        Action<PipeConfigurator<ConsumeContext<TMessage>>>? configurePipe,
        bool endpointNameIsExplicit,
        Type? endpointNameFormatterType)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        return RegisterConsumer(
            typeof(TConsumer),
            queueName,
            configurePipe,
            new ConsumerRegistrationDescriptor<TConsumer, TMessage>(),
            endpointNameIsExplicit,
            endpointNameFormatterType,
            definition: null,
            typeof(TMessage));
    }

    [RequiresDynamicCode("Reflection-based consumer methods close generic registration descriptors at runtime. Use generated consumer registration for NativeAOT.")]
    [RequiresUnreferencedCode("Reflection-based consumer methods require method and parameter metadata. Use generated consumer registration for trimmed applications.")]
    internal void RegisterConsumerMethod(ConsumerMethodDefinition definition, string? endpointName = null)
    {
        RegisterConsumer(
            definition.Method.DeclaringType!,
            endpointName ?? definition.EndpointName,
            configurePipe: null,
            ReflectionConsumerMethodRegistrationDescriptorFactory.Create(definition),
            endpointNameIsExplicit: endpointName is not null || definition.EndpointNameIsExplicit,
            endpointNameFormatterType: endpointName is not null ? null : definition.EndpointNameFormatterType,
            definition: null,
            messageTypes: [definition.MessageType]);
    }

    private ConsumerDefinitionModel RegisterConsumer(
        Type consumerType,
        string queueName,
        Delegate? configurePipe,
        IConsumerRegistrationDescriptor? registration,
        bool endpointNameIsExplicit,
        Type? endpointNameFormatterType,
        IConsumerDefinition? definition,
        params Type[] messageTypes)
    {
        var model = CreateConsumerDefinition(
            consumerType,
            queueName,
            endpointNameIsExplicit,
            endpointNameFormatterType,
            definition,
            messageTypes);
        MaterializeConsumer(model, configurePipe, registration, model.MessageTypes);
        return model;
    }

    private ConsumerDefinitionModel CreateConsumerDefinition(
        Type consumerType,
        string queueName,
        bool endpointNameIsExplicit,
        Type? endpointNameFormatterType,
        IConsumerDefinition? definition,
        IEnumerable<Type> messageTypes)
    {
        var model = new ConsumerDefinitionModel(
            consumerType,
            queueName,
            endpointNameIsExplicit,
            endpointNameFormatterType,
            messageTypes,
            definition?.ConcurrentMessageLimit);
        consumerDefinitions.Add(model);
        return model;
    }

    private void MaterializeConsumer(
        ConsumerDefinitionModel model,
        Delegate? configurePipe,
        IConsumerRegistrationDescriptor? registration,
        IEnumerable<Type> messageTypes)
    {
        EnsureReceiveEndpoint(model.EndpointName);
        var bindings = messageTypes.Select(mt =>
        {
            var msg = Messages.FirstOrDefault(m => m.MessageType == mt) ?? RegisterMessage(mt);
            return new MessageBinding { MessageType = mt, EntityName = msg.EntityName };
        }).ToList();

        Consumers.Add(new ConsumerTopology
        {
            Definition = model,
            ConsumerType = model.ConsumerType,
            QueueName = model.EndpointName,
            EndpointNameIsExplicit = model.EndpointNameIsExplicit,
            EndpointNameFormatterType = model.EndpointNameFormatterType,
            Bindings = bindings,
            ConfigurePipe = configurePipe,
            Registration = registration,
            ConcurrentMessageLimit = model.ConcurrentMessageLimit
        });
    }

    public void MoveConsumerToEndpoint(ConsumerTopology consumer, string endpointName)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        var previousEndpointName = consumer.QueueName;
        consumer.QueueName = endpointName;
        EnsureReceiveEndpoint(endpointName);

        if (!string.Equals(previousEndpointName, endpointName, StringComparison.Ordinal)
            && !Consumers.Any(x => !ReferenceEquals(x, consumer) && x.QueueName == previousEndpointName))
        {
            _receiveEndpoints.RemoveAll(x => x.Name == previousEndpointName);
        }
    }

    private void EnsureReceiveEndpoint(string endpointName)
    {
        if (_receiveEndpoints.Any(x => x.Name == endpointName))
            return;

        _receiveEndpoints.Add(new ReceiveEndpointDefinition(endpointName, Durable: true, Temporary: false));
    }
}

internal static class ReflectionConsumerRegistrationDescriptorFactory
{
    [RequiresDynamicCode("Closing a consumer registration descriptor at runtime requires dynamic generic code. Use typed or generated consumer registration for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime consumer discovery cannot guarantee that consumer constructors and generic interfaces are preserved. Use generated consumer registration for trimmed applications.")]
    public static IConsumerRegistrationDescriptor? Create(Type consumerType, Type messageType)
    {
        if (!typeof(IConsumer).IsAssignableFrom(consumerType))
            return null;

        var descriptorType = typeof(ConsumerRegistrationDescriptor<,>).MakeGenericType(consumerType, messageType);
        return (IConsumerRegistrationDescriptor)(Activator.CreateInstance(descriptorType)
            ?? throw new InvalidOperationException($"Failed to create a registration descriptor for {consumerType}."));
    }
}
