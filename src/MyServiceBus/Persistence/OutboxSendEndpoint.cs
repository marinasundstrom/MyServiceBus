using System.Diagnostics;
using MyServiceBus.Serialization;

namespace MyServiceBus.Persistence;

internal sealed class OutboxSendEndpoint : ISendEndpoint
{
    private readonly OutboxSession session;
    private readonly ISendEndpoint fallback;
    private readonly ISendPipe sendPipe;
    private readonly IMessageSerializer serializer;
    private readonly Uri destination;
    private readonly Uri source;
    private readonly ISendContextFactory contextFactory;
    private readonly Action? ensureStarted;

    public OutboxSendEndpoint(
        OutboxSession session,
        ISendEndpoint fallback,
        ISendPipe sendPipe,
        IMessageSerializer serializer,
        Uri destination,
        Uri source,
        ISendContextFactory contextFactory,
        Action? ensureStarted)
    {
        this.session = session;
        this.fallback = fallback;
        this.sendPipe = sendPipe;
        this.serializer = serializer;
        this.destination = destination;
        this.source = source;
        this.contextFactory = contextFactory;
        this.ensureStarted = ensureStarted;
    }

    public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => Send<T>((object)message, contextCallback, cancellationToken);

    public async Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class
    {
        if (session.Writer is not { } writer)
        {
            await fallback.Send<T>(message, contextCallback, cancellationToken);
            return;
        }

        ensureStarted?.Invoke();
        var context = contextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), serializer, cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = source;
        context.DestinationAddress = destination;
        contextCallback?.Invoke(context);

        await sendPipe.Send(context);
        var typed = message is T value ? value : (T)MessageProxy.Create(typeof(T), message);
        await writer.AddAsync(OutboxMessageFactory.Create(typed, context), cancellationToken);
    }
}
