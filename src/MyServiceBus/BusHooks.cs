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
    string? SpanId) : BusHookEvent(OccurredAtUtc)
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
        string? conversationId = null)
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
            activity?.SpanId.ToString());
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
                context.ConversationId?.ToString()));
        }
    }
}
