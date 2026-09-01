using System.Collections.Concurrent;
using System.Reflection;

namespace MyServiceBus.Orchestration;

internal sealed class SagaStateMachineConsumer<TStateMachine, TSaga, TMessage> : IConsumer<TMessage>
    where TStateMachine : SagaStateMachine<TSaga>
    where TSaga : class
    where TMessage : class
{
    private readonly SagaStateMachineRuntime<TSaga> runtime;

    public SagaStateMachineConsumer(SagaStateMachineRuntime<TSaga> runtime)
    {
        this.runtime = runtime;
    }

    public Task Consume(ConsumeContext<TMessage> context)
        => runtime.Deliver(
            context.Message,
            (operation, cancellationToken) => SagaBusOutgoingDispatcher.Dispatch(
                context,
                operation,
                cancellationToken),
            context.CancellationToken).AsTask();
}

internal static class SagaBusOutgoingDispatcher
{
    private static readonly ConcurrentDictionary<Type, SagaTypedOutgoingDispatcher> Dispatchers = new();
    private static readonly MethodInfo CreateDispatcherMethod = typeof(SagaBusOutgoingDispatcher)
        .GetMethod(nameof(CreateTypedDispatcher), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ValueTask Dispatch(
        ConsumeContext context,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken)
    {
        var dispatcher = Dispatchers.GetOrAdd(operation.Message.GetType(), CreateDispatcher);
        return new ValueTask(dispatcher(context, operation, cancellationToken));
    }

    private static SagaTypedOutgoingDispatcher CreateDispatcher(Type messageType)
        => (SagaTypedOutgoingDispatcher)CreateDispatcherMethod
            .MakeGenericMethod(messageType)
            .Invoke(null, null)!;

    private static SagaTypedOutgoingDispatcher CreateTypedDispatcher<TMessage>()
        where TMessage : class
        => DispatchTyped<TMessage>;

    private static Task DispatchTyped<TMessage>(
        ConsumeContext context,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken)
        where TMessage : class
    {
        return operation.Kind switch
        {
            SagaActivityKind.Send => context.Send<TMessage>(
                new Uri(operation.Destination!, UriKind.RelativeOrAbsolute),
                operation.Message,
                cancellationToken: cancellationToken),
            SagaActivityKind.Publish => context.Publish<TMessage>(
                operation.Message,
                cancellationToken: cancellationToken),
            _ => throw new InvalidOperationException(
                $"Saga outgoing operation '{operation.Kind}' cannot be dispatched through the bus.")
        };
    }

    private delegate Task SagaTypedOutgoingDispatcher(
        ConsumeContext context,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken);
}
