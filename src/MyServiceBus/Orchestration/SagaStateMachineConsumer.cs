using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace MyServiceBus.Orchestration;

internal sealed class SagaStateMachineConsumer<TStateMachine, TSaga, TMessage> : IConsumer<TMessage>
    where TStateMachine : SagaStateMachine<TSaga>
    where TSaga : class
    where TMessage : class
{
    private readonly SagaStateMachineRuntime<TSaga> runtime;
    private readonly SagaStateMachineDefinition definition;
    private readonly string eventId;
    private readonly Func<TMessage, Guid> correlate;
    private readonly ISendEndpointProvider sendEndpointProvider;
    private readonly IPublishEndpoint publishEndpoint;
    private readonly IBusHookDispatcher hooks;

    public SagaStateMachineConsumer(
        SagaStateMachineRuntime<TSaga> runtime,
        SagaStateMachineDefinition definition,
        string eventId,
        Func<TMessage, Guid> correlate,
        ISendEndpointProvider sendEndpointProvider,
        IPublishEndpoint publishEndpoint,
        IEnumerable<IBusHook> hooks)
    {
        this.runtime = runtime;
        this.definition = definition;
        this.eventId = eventId;
        this.correlate = correlate;
        this.sendEndpointProvider = sendEndpointProvider;
        this.publishEndpoint = publishEndpoint;
        this.hooks = new BusHookDispatcher(hooks);
    }

    public async Task Consume(ConsumeContext<TMessage> context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var result = await runtime.Deliver(
                context.Message,
                (operation, cancellationToken) => SagaBusOutgoingDispatcher.Dispatch(
                    sendEndpointProvider,
                    publishEndpoint,
                    context,
                    operation,
                    cancellationToken),
                context.CancellationToken).ConfigureAwait(false);
            hooks.Dispatch(CreateHookEvent(result, context, Stopwatch.GetElapsedTime(startedAt), null));
        }
        catch (Exception exception)
        {
            hooks.Dispatch(CreateHookEvent(
                null,
                context,
                Stopwatch.GetElapsedTime(startedAt),
                exception,
                TryCorrelate(context.Message)));
            throw;
        }
    }

    private SagaStateMachineHookEvent CreateHookEvent(
        SagaDeliveryResult? result,
        ConsumeContext context,
        TimeSpan duration,
        Exception? exception,
        Guid? failedCorrelationId = null)
        => new(
            DateTimeOffset.UtcNow,
            exception is null,
            duration.TotalMilliseconds,
            definition.StateMachineId,
            definition.DefinitionVersion,
            definition.Owner,
            eventId,
            result?.Status switch
            {
                SagaDeliveryStatus.Consumed => "consumed",
                SagaDeliveryStatus.Ignored => "ignored",
                SagaDeliveryStatus.MissingDiscarded => "missing-discarded",
                _ => "faulted"
            },
            result?.CorrelationId ?? failedCorrelationId,
            result?.BeginState,
            result?.EndState,
            result?.Created ?? false,
            result?.Completed ?? false,
            result?.InstancePresent ?? false,
            exception?.GetType().FullName,
            exception?.Message,
            context.MessageId?.ToString());

    private Guid? TryCorrelate(TMessage message)
    {
        try
        {
            var correlationId = correlate(message);
            return correlationId == Guid.Empty ? null : correlationId;
        }
        catch
        {
            return null;
        }
    }
}

internal static class SagaBusOutgoingDispatcher
{
    private static readonly ConcurrentDictionary<Type, SagaTypedOutgoingDispatcher> Dispatchers = new();
    private static readonly MethodInfo CreateDispatcherMethod = typeof(SagaBusOutgoingDispatcher)
        .GetMethod(nameof(CreateTypedDispatcher), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ValueTask Dispatch(
        ISendEndpointProvider sendEndpointProvider,
        IPublishEndpoint publishEndpoint,
        ConsumeContext context,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken)
    {
        var dispatcher = Dispatchers.GetOrAdd(operation.Message.GetType(), CreateDispatcher);
        return new ValueTask(dispatcher(
            sendEndpointProvider,
            publishEndpoint,
            context,
            operation,
            cancellationToken));
    }

    private static SagaTypedOutgoingDispatcher CreateDispatcher(Type messageType)
        => (SagaTypedOutgoingDispatcher)CreateDispatcherMethod
            .MakeGenericMethod(messageType)
            .Invoke(null, null)!;

    private static SagaTypedOutgoingDispatcher CreateTypedDispatcher<TMessage>()
        where TMessage : class
        => DispatchTyped<TMessage>;

    private static Task DispatchTyped<TMessage>(
        ISendEndpointProvider sendEndpointProvider,
        IPublishEndpoint publishEndpoint,
        ConsumeContext context,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken)
        where TMessage : class
    {
        return operation.Kind switch
        {
            SagaActivityKind.Send => Send<TMessage>(
                sendEndpointProvider,
                context,
                operation,
                cancellationToken),
            SagaActivityKind.Publish => publishEndpoint.Publish<TMessage>(
                operation.Message,
                sendContext => ApplyConsumeMetadata(sendContext, context),
                cancellationToken: cancellationToken),
            _ => throw new InvalidOperationException(
                $"Saga outgoing operation '{operation.Kind}' cannot be dispatched through the bus.")
        };
    }

    private static async Task Send<TMessage>(
        ISendEndpointProvider sendEndpointProvider,
        ConsumeContext context,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken)
        where TMessage : class
    {
        var endpoint = await sendEndpointProvider.GetSendEndpoint(
            new Uri(operation.Destination!, UriKind.RelativeOrAbsolute)).ConfigureAwait(false);
        await endpoint.Send<TMessage>(
            operation.Message,
            sendContext => ApplyConsumeMetadata(sendContext, context),
            cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyConsumeMetadata(ISendContext sendContext, ConsumeContext consumeContext)
    {
        sendContext.RequestId = consumeContext.RequestId;
        sendContext.CorrelationId = consumeContext.CorrelationId?.ToString();
        sendContext.ConversationId = consumeContext.ConversationId;
        sendContext.InitiatorId = consumeContext.CorrelationId;
        if (sendContext is SendContext concrete)
            concrete.CausationMessageId = consumeContext.MessageId;
    }

    private delegate Task SagaTypedOutgoingDispatcher(
        ISendEndpointProvider sendEndpointProvider,
        IPublishEndpoint publishEndpoint,
        ConsumeContext context,
        SagaOutgoingOperation operation,
        CancellationToken cancellationToken);
}
