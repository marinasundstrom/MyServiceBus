using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.Topology;

internal sealed class ReflectionConsumerMethodRegistrationDescriptor<TMessage> : IConsumerRegistrationDescriptor
    where TMessage : class
{
    private readonly MethodInfo method;
    private readonly ConsumerMethodParameterBinding[] bindings;
    private readonly string consumerId;

    public ReflectionConsumerMethodRegistrationDescriptor(
        MethodInfo method,
        ConsumerMethodParameterBinding[] bindings,
        string consumerId)
    {
        this.method = method;
        this.bindings = bindings;
        this.consumerId = consumerId;
    }

    public Type ConsumerType => method.DeclaringType!;

    public Type MessageType => typeof(TMessage);

    public Task Register(
        IMessageBus bus,
        ConsumerTopology consumer,
        CancellationToken cancellationToken = default)
    {
        if (bus is not IConsumerMethodConnector connector)
            throw new NotSupportedException($"{bus.GetType()} does not support method-based consumers.");

        return connector.AddConsumerMethod<TMessage>(consumer, consumerId, Invoke, cancellationToken);
    }

    public Delegate CreateRetryConfiguration(int retryCount, TimeSpan? retryDelay)
    {
        void Configure(PipeConfigurator<ConsumeContext<TMessage>> pipe) => pipe.UseRetry(retryCount, retryDelay);
        return (Action<PipeConfigurator<ConsumeContext<TMessage>>>)Configure;
    }

    private async Task Invoke(IServiceProvider provider, ConsumeContext<TMessage> context)
    {
        var parameters = method.GetParameters();
        var arguments = new object?[bindings.Length];
        for (var index = 0; index < bindings.Length; index++)
        {
            arguments[index] = bindings[index] switch
            {
                ConsumerMethodParameterBinding.Message => context.Message,
                ConsumerMethodParameterBinding.ConsumeContext => context,
                ConsumerMethodParameterBinding.CancellationToken => context.CancellationToken,
                ConsumerMethodParameterBinding.Service => provider.GetRequiredService(parameters[index].ParameterType),
                _ => throw new InvalidOperationException($"Unsupported binding for {method}.")
            };
        }

        var target = method.IsStatic ? null : provider.GetRequiredService(method.DeclaringType!);
        object? result;
        try
        {
            result = method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                break;
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                break;
        }
    }
}

internal enum ConsumerMethodParameterBinding
{
    Message,
    ConsumeContext,
    CancellationToken,
    Service
}

internal sealed class ConsumerMethodDefinition
{
    public ConsumerMethodDefinition(
        MethodInfo method,
        Type messageType,
        string endpointName,
        bool endpointNameIsExplicit,
        Type? endpointNameFormatterType,
        ConsumerMethodParameterBinding[] bindings)
    {
        Method = method;
        MessageType = messageType;
        EndpointName = endpointName;
        EndpointNameIsExplicit = endpointNameIsExplicit;
        EndpointNameFormatterType = endpointNameFormatterType;
        Bindings = bindings;
    }

    public MethodInfo Method { get; }
    public Type MessageType { get; }
    public string EndpointName { get; }
    public bool EndpointNameIsExplicit { get; }
    public Type? EndpointNameFormatterType { get; }
    public ConsumerMethodParameterBinding[] Bindings { get; }
}

internal static class ReflectionConsumerMethodDiscovery
{
    public static IEnumerable<ConsumerMethodDefinition> Discover(Assembly assembly)
        => Discover(assembly, static _ => true);

    public static IEnumerable<ConsumerMethodDefinition> Discover(Assembly assembly, Func<Type, bool> typeFilter)
    {
        foreach (var type in assembly.GetTypes().Where(typeFilter).Where(static type => type.IsClass && (!type.IsAbstract || type.IsSealed)))
        {
            var typeAttribute = type.GetCustomAttribute<ConsumerAttribute>();
            if (typeAttribute is not null && ImplementsGenericConsumer(type))
                typeAttribute = null;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var methodAttribute = method.GetCustomAttribute<ConsumerAttribute>();
                if (methodAttribute is null && typeAttribute is null)
                    continue;
                if (method.IsPrivate || method.IsFamily || method.IsFamilyAndAssembly || method.IsAbstract || method.IsGenericMethodDefinition)
                    continue;

                yield return CreateDefinition(method, methodAttribute, typeAttribute);
            }
        }
    }

    private static bool ImplementsGenericConsumer(Type type)
        => type.GetInterfaces().Any(static implementedInterface =>
            implementedInterface.IsGenericType
            && implementedInterface.GetGenericTypeDefinition() == typeof(IConsumer<>));

    public static IEnumerable<ConsumerMethodDefinition> Discover(Type consumerType)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        if (!consumerType.IsClass || consumerType.IsAbstract && !consumerType.IsSealed)
            throw new ArgumentException($"Consumer method type {consumerType} must be a class or static class.", nameof(consumerType));

        var typeAttribute = ImplementsGenericConsumer(consumerType)
            ? null
            : consumerType.GetCustomAttribute<ConsumerAttribute>();

        foreach (var method in consumerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.IsPrivate || method.IsFamily || method.IsFamilyAndAssembly || method.IsAbstract || method.IsGenericMethodDefinition)
                continue;
            yield return CreateDefinition(method, method.GetCustomAttribute<ConsumerAttribute>(), typeAttribute);
        }
    }

    private static ConsumerMethodDefinition CreateDefinition(
        MethodInfo method,
        ConsumerAttribute? methodAttribute,
        ConsumerAttribute? typeAttribute)
    {
        if (method.ReturnType != typeof(void)
            && method.ReturnType != typeof(Task)
            && method.ReturnType != typeof(ValueTask))
        {
            throw Invalid(method, "return type must be void, Task, or ValueTask");
        }

        Type? messageType = null;
        var parameters = method.GetParameters();
        var bindings = new ConsumerMethodParameterBinding[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            if (parameter.ParameterType.IsByRef || parameter.IsOut)
                throw Invalid(method, $"parameter '{parameter.Name}' cannot be passed by reference");

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                bindings[index] = ConsumerMethodParameterBinding.CancellationToken;
            }
            else if (parameter.ParameterType == typeof(ConsumeContext)
                || parameter.ParameterType.IsGenericType
                && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ConsumeContext<>))
            {
                bindings[index] = ConsumerMethodParameterBinding.ConsumeContext;
            }
            else if (messageType is null)
            {
                messageType = parameter.ParameterType;
                bindings[index] = ConsumerMethodParameterBinding.Message;
            }
            else
            {
                bindings[index] = ConsumerMethodParameterBinding.Service;
            }
        }

        if (messageType is null || !messageType.IsClass)
            throw Invalid(method, "exactly one reference-type message parameter is required");

        foreach (var parameter in parameters)
        {
            if (parameter.ParameterType.IsGenericType
                && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ConsumeContext<>)
                && parameter.ParameterType.GetGenericArguments()[0] != messageType)
            {
                throw Invalid(method, $"context type must be ConsumeContext<{messageType.Name}>");
            }
        }

        var methodEndpointName = methodAttribute?.EndpointName;
        var typeEndpointName = typeAttribute?.EndpointName;
        var endpointName = methodEndpointName
            ?? typeEndpointName
            ?? (methodAttribute is not null ? method.Name : method.DeclaringType!.Name);
        var endpointNameIsExplicit = methodEndpointName is not null || typeEndpointName is not null;
        var endpointNameFormatterType = endpointNameIsExplicit || methodAttribute is not null
            ? null
            : method.DeclaringType;
        return new ConsumerMethodDefinition(
            method,
            messageType,
            endpointName,
            endpointNameIsExplicit,
            endpointNameFormatterType,
            bindings);
    }

    private static InvalidOperationException Invalid(MethodInfo method, string reason)
        => new($"Consumer method '{method.DeclaringType?.FullName}.{method.Name}' is invalid: {reason}.");
}

internal static class ReflectionConsumerMethodRegistrationDescriptorFactory
{
    public static IConsumerRegistrationDescriptor Create(ConsumerMethodDefinition definition)
    {
        var descriptorType = typeof(ReflectionConsumerMethodRegistrationDescriptor<>).MakeGenericType(definition.MessageType);
        var consumerId = $"{definition.Method.Module.ModuleVersionId:N}:{definition.Method.MetadataToken}";
        return (IConsumerRegistrationDescriptor)(Activator.CreateInstance(
            descriptorType,
            definition.Method,
            definition.Bindings,
            consumerId) ?? throw new InvalidOperationException($"Failed to create a registration descriptor for {definition.Method}."));
    }
}
