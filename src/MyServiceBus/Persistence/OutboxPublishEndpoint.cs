using MyServiceBus.Serialization;

namespace MyServiceBus.Persistence;

internal sealed class OutboxPublishEndpoint : IPublishEndpoint
{
    private readonly OutboxSession session;
    private readonly IPublishEndpoint fallback;
    private readonly ITransportFactory transportFactory;
    private readonly ISendPipe sendPipe;
    private readonly IPublishPipe publishPipe;
    private readonly IMessageSerializer serializer;
    private readonly Uri source;
    private readonly IPublishContextFactory contextFactory;
    private readonly Action? ensureStarted;

    public OutboxPublishEndpoint(
        OutboxSession session,
        IPublishEndpoint fallback,
        ITransportFactory transportFactory,
        ISendPipe sendPipe,
        IPublishPipe publishPipe,
        IMessageSerializer serializer,
        Uri source,
        IPublishContextFactory contextFactory,
        Action? ensureStarted)
    {
        this.session = session;
        this.fallback = fallback;
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.publishPipe = publishPipe;
        this.serializer = serializer;
        this.source = source;
        this.contextFactory = contextFactory;
        this.ensureStarted = ensureStarted;
    }

    public Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => Publish<T>((object)message, contextCallback, cancellationToken);

    public async Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class
    {
        if (session.Writer is not { } writer)
        {
            await fallback.Publish<T>(message, contextCallback, cancellationToken);
            return;
        }

        ensureStarted?.Invoke();
        var typed = message is T value ? value : (T)MessageProxy.Create(typeof(T), message);
        var destination = transportFactory.GetPublishAddress(typeof(T));
        var context = contextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), serializer, cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = source;
        context.DestinationAddress = destination;
        context.RoutingKey = transportFactory.GetPublishEntityName(typeof(T));
        contextCallback?.Invoke(context);
        if (context.ScheduledEnqueueTime is not null)
            throw new NotSupportedException("Scheduled messages cannot yet be captured by the transactional outbox.");

        await publishPipe.Send(context);
        await sendPipe.Send(context);
        await writer.AddAsync(OutboxMessageFactory.Create(typed, context), cancellationToken);
    }
}
