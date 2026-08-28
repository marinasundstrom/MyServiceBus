using System.Diagnostics.CodeAnalysis;
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
    private readonly IConsumerMethodResponseHandler? responseHandler;

    public ReflectionConsumerMethodRegistrationDescriptor(
        MethodInfo method,
        ConsumerMethodParameterBinding[] bindings,
        string consumerId,
        IConsumerMethodResponseHandler? responseHandler)
    {
        this.method = method;
        this.bindings = bindings;
        this.consumerId = consumerId;
        this.responseHandler = responseHandler;
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

        if (responseHandler is not null)
        {
            await responseHandler.Respond(result, context).ConfigureAwait(false);
            return;
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

internal enum ConsumerMethodReturnKind
{
    Void,
    Task,
    ValueTask,
    TaskResponse,
    ValueTaskResponse
}

internal interface IConsumerMethodResponseHandler
{
    Task Respond(object? result, ConsumeContext context);
}

internal sealed class TaskConsumerMethodResponseHandler<TResponse> : IConsumerMethodResponseHandler
    where TResponse : class
{
    public async Task Respond(object? result, ConsumeContext context)
    {
        if (result is not Task<TResponse> responseTask)
            throw new InvalidOperationException($"Consumer method did not return Task<{typeof(TResponse)}>.");

        var response = await responseTask.ConfigureAwait(false);
        await context.RespondAsync(response, null, context.CancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ValueTaskConsumerMethodResponseHandler<TResponse> : IConsumerMethodResponseHandler
    where TResponse : class
{
    public async Task Respond(object? result, ConsumeContext context)
    {
        if (result is not ValueTask<TResponse> responseTask)
            throw new InvalidOperationException($"Consumer method did not return ValueTask<{typeof(TResponse)}>.");

        var response = await responseTask.ConfigureAwait(false);
        await context.RespondAsync(response, null, context.CancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ConsumerMethodDefinition
{
    public ConsumerMethodDefinition(
        MethodInfo method,
        Type messageType,
        string endpointName,
        bool endpointNameIsExplicit,
        Type? endpointNameFormatterType,
        ConsumerMethodParameterBinding[] bindings,
        ConsumerMethodReturnKind returnKind,
        Type? responseType)
    {
        Method = method;
        MessageType = messageType;
        EndpointName = endpointName;
        EndpointNameIsExplicit = endpointNameIsExplicit;
        EndpointNameFormatterType = endpointNameFormatterType;
        Bindings = bindings;
        ReturnKind = returnKind;
        ResponseType = responseType;
    }

    public MethodInfo Method { get; }
    public Type MessageType { get; }
    public string EndpointName { get; }
    public bool EndpointNameIsExplicit { get; }
    public Type? EndpointNameFormatterType { get; }
    public ConsumerMethodParameterBinding[] Bindings { get; }
    public ConsumerMethodReturnKind ReturnKind { get; }
    public Type? ResponseType { get; }
}

internal static class ReflectionConsumerMethodDiscovery
{
    [RequiresUnreferencedCode("Attributed consumer method discovery requires method and parameter metadata.")]
    public static IEnumerable<ConsumerMethodDefinition> Discover(Assembly assembly)
        => Discover(assembly, static _ => true);

    [RequiresUnreferencedCode("Attributed consumer method discovery requires method and parameter metadata.")]
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

    [RequiresUnreferencedCode("Consumer method discovery requires method and parameter metadata.")]
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
        var (returnKind, responseType) = GetReturnShape(method);

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
            bindings,
            returnKind,
            responseType);
    }

    private static (ConsumerMethodReturnKind ReturnKind, Type? ResponseType) GetReturnShape(MethodInfo method)
    {
        if (method.ReturnType == typeof(void))
            return (ConsumerMethodReturnKind.Void, null);
        if (method.ReturnType == typeof(Task))
            return (ConsumerMethodReturnKind.Task, null);
        if (method.ReturnType == typeof(ValueTask))
            return (ConsumerMethodReturnKind.ValueTask, null);

        if (method.ReturnType.IsGenericType)
        {
            var genericType = method.ReturnType.GetGenericTypeDefinition();
            var responseType = method.ReturnType.GetGenericArguments()[0];
            if (responseType.IsValueType)
                throw Invalid(method, "response type must be a reference type");
            if (genericType == typeof(Task<>))
                return (ConsumerMethodReturnKind.TaskResponse, responseType);
            if (genericType == typeof(ValueTask<>))
                return (ConsumerMethodReturnKind.ValueTaskResponse, responseType);
        }

        throw Invalid(method, "return type must be void, Task, ValueTask, Task<TResponse>, or ValueTask<TResponse>");
    }

    private static InvalidOperationException Invalid(MethodInfo method, string reason)
        => new($"Consumer method '{method.DeclaringType?.FullName}.{method.Name}' is invalid: {reason}.");
}

internal static class ReflectionConsumerMethodRegistrationDescriptorFactory
{
    [RequiresDynamicCode("Reflection-based consumer methods close generic registration descriptors at runtime. Use generated consumer registration for NativeAOT.")]
    [RequiresUnreferencedCode("Reflection-based consumer methods require method and parameter metadata. Use generated consumer registration for trimmed applications.")]
    public static IConsumerRegistrationDescriptor Create(ConsumerMethodDefinition definition)
    {
        var descriptorType = typeof(ReflectionConsumerMethodRegistrationDescriptor<>).MakeGenericType(definition.MessageType);
        var consumerId = $"{definition.Method.Module.ModuleVersionId:N}:{definition.Method.MetadataToken}";
        var responseHandler = CreateResponseHandler(definition);
        return (IConsumerRegistrationDescriptor)(Activator.CreateInstance(
            descriptorType,
            definition.Method,
            definition.Bindings,
            consumerId,
            responseHandler) ?? throw new InvalidOperationException($"Failed to create a registration descriptor for {definition.Method}."));
    }

    private static IConsumerMethodResponseHandler? CreateResponseHandler(ConsumerMethodDefinition definition)
    {
        if (definition.ResponseType is null)
            return null;

        var handlerType = definition.ReturnKind switch
        {
            ConsumerMethodReturnKind.TaskResponse => typeof(TaskConsumerMethodResponseHandler<>),
            ConsumerMethodReturnKind.ValueTaskResponse => typeof(ValueTaskConsumerMethodResponseHandler<>),
            _ => null
        };
        return handlerType is null
            ? null
            : (IConsumerMethodResponseHandler?)Activator.CreateInstance(handlerType.MakeGenericType(definition.ResponseType));
    }
}
