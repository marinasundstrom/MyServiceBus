using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public interface IBusHook
{
    void Handle(BusHookEvent busEvent);
}

public interface IBusHookDispatcher
{
    bool IsEnabled { get; }

    void Dispatch(BusHookEvent busEvent);
}

public abstract record BusHookEvent(DateTimeOffset OccurredAtUtc);

public sealed record BusLifecycleHookEvent(
    DateTimeOffset OccurredAtUtc,
    string State,
    string BusAddress) : BusHookEvent(OccurredAtUtc);

public sealed record MessageOperationHookEvent(
    DateTimeOffset OccurredAtUtc,
    string Kind,
    bool Succeeded,
    string MessageType,
    string MessageUrn,
    string? EndpointName,
    string? DestinationAddress,
    double DurationMs,
    string? ExceptionType,
    string? ExceptionMessage,
    string? CorrelationId,
    string? ConversationId,
    string? TraceId,
    string? SpanId,
    int? RetryAttempt,
    int? RetryLimit,
    string? MessageId,
    string? CausationMessageId = null,
    string? RequestId = null,
    string? ResponseAddress = null,
    string? MessageIntent = null,
    object? Message = null) : BusHookEvent(OccurredAtUtc)
{
    public static MessageOperationHookEvent Create(
        string kind,
        bool succeeded,
        string messageType,
        string messageUrn,
        string? endpointName,
        string? destinationAddress,
        TimeSpan duration,
        Exception? exception = null,
        string? correlationId = null,
        string? conversationId = null,
        int? retryAttempt = null,
        int? retryLimit = null,
        string? messageId = null,
        string? causationMessageId = null,
        string? requestId = null,
        string? responseAddress = null,
        string? messageIntent = null,
        object? message = null)
    {
        var activity = Activity.Current;
        return new MessageOperationHookEvent(
            DateTimeOffset.UtcNow,
            kind,
            succeeded,
            messageType,
            messageUrn,
            endpointName,
            destinationAddress,
            duration.TotalMilliseconds,
            exception?.GetType().FullName,
            exception?.Message,
            correlationId,
            conversationId,
            activity?.TraceId.ToString(),
            activity?.SpanId.ToString(),
            retryAttempt,
            retryLimit,
            messageId,
            causationMessageId,
            requestId,
            responseAddress,
            messageIntent,
            message);
    }
}

public sealed record OutboxDeliveryHookEvent(
    DateTimeOffset OccurredAtUtc,
    string ServiceName,
    string OwnerId,
    bool Succeeded,
    double DurationMs,
    int BatchLeased,
    int BatchDispatched,
    int BatchFailed,
    int BatchLostLeases,
    int? Pending,
    int? Leased,
    int? Retrying,
    int? StoredDispatched,
    int? Dead,
    int? Cancelled,
    double? OldestUndispatchedAgeMs,
    string? FailureCategory) : BusHookEvent(OccurredAtUtc);

public sealed record SagaStateMachineHookEvent(
    DateTimeOffset OccurredAtUtc,
    bool Succeeded,
    double DurationMs,
    string StateMachineId,
    string DefinitionVersion,
    string Owner,
    string EventId,
    string Status,
    Guid? SagaCorrelationId,
    string? BeginState,
    string? EndState,
    bool Created,
    bool Completed,
    bool InstancePresent,
    string? ExceptionType,
    string? ExceptionMessage,
    string? MessageId) : BusHookEvent(OccurredAtUtc);

internal sealed class BusHookRetryObserver : IRetryObserver
{
    private readonly IBusHookDispatcher dispatcher;

    public BusHookRetryObserver(IBusHookDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public void Observe(RetryEvent retryEvent)
    {
        if (!dispatcher.IsEnabled || retryEvent.Context is not ConsumeContext context)
            return;

        var message = retryEvent.Context.GetType().GetProperty("Message")?.GetValue(retryEvent.Context);
        if (message is null)
            return;

        var messageType = message.GetType();
        dispatcher.Dispatch(MessageOperationHookEvent.Create(
            retryEvent.Exhausted ? "retry_exhausted" : "retry_attempted",
            false,
            messageType.FullName ?? messageType.Name,
            MessageUrn.For(messageType),
            null,
            null,
            TimeSpan.Zero,
            retryEvent.Exception,
            context.CorrelationId?.ToString(),
            context.ConversationId?.ToString(),
            retryEvent.Attempt,
            retryEvent.RetryLimit,
            context.MessageId?.ToString(),
            requestId: context.RequestId?.ToString(),
            message: message));
    }
}

internal sealed class BusHookDispatcher : IBusHookDispatcher
{
    private readonly IReadOnlyList<IBusHook> hooks;
    private readonly ILogger<BusHookDispatcher>? logger;

    public BusHookDispatcher(IEnumerable<IBusHook> hooks, ILogger<BusHookDispatcher>? logger = null)
    {
        this.hooks = hooks.ToArray();
        this.logger = logger;
    }

    public bool IsEnabled => hooks.Count > 0;

    public void Dispatch(BusHookEvent busEvent)
    {
        foreach (var hook in hooks)
        {
            try
            {
                hook.Handle(busEvent);
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception, "MyServiceBus hook {HookType} failed", hook.GetType().FullName);
            }
        }
    }
}

internal sealed class BusHookConsumeFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private readonly IBusHookDispatcher dispatcher;
    private readonly string endpointName;

    public BusHookConsumeFilter(IBusHookDispatcher dispatcher, string endpointName)
    {
        this.dispatcher = dispatcher;
        this.endpointName = endpointName;
    }

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        if (!dispatcher.IsEnabled)
        {
            await next.Send(context).ConfigureAwait(false);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await next.Send(context).ConfigureAwait(false);
            Dispatch(succeeded: true, exception: null);
        }
        catch (Exception exception)
        {
            Dispatch(succeeded: false, exception);
            throw;
        }

        void Dispatch(bool succeeded, Exception? exception)
        {
            dispatcher.Dispatch(MessageOperationHookEvent.Create(
                succeeded ? "consumed" : "consume_faulted",
                succeeded,
                typeof(TMessage).FullName ?? typeof(TMessage).Name,
                MessageUrn.For(typeof(TMessage)),
                endpointName,
                null,
                Stopwatch.GetElapsedTime(startedAt),
                exception,
                context.CorrelationId?.ToString(),
                context.ConversationId?.ToString(),
                messageId: context.MessageId?.ToString(),
                requestId: context.RequestId?.ToString(),
                message: context.Message));
        }
    }
}
