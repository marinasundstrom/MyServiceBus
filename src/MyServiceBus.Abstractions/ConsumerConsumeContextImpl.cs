using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class ConsumerConsumeContextImpl<TConsumer, TMessage> : ConsumerConsumeContext<TConsumer, TMessage>
    where TConsumer : class
    where TMessage : class
{
    readonly ConsumeContext<TMessage> context;

    public ConsumerConsumeContextImpl(TConsumer consumer, ConsumeContext<TMessage> context)
    {
        Consumer = consumer;
        this.context = context;
    }

    public TConsumer Consumer { get; }
    public TMessage Message => context.Message;
    CancellationToken PipeContext.CancellationToken => context.CancellationToken;

    public Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => context.Publish<T>(message, contextCallback, cancellationToken);

    public Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => context.Publish(message, contextCallback, cancellationToken);

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri) => context.GetSendEndpoint(uri);

    public Task Send<T>(Uri address, T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => context.Send(address, message, contextCallback, cancellationToken);

    public Task Send<T>(Uri address, object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => context.Send<T>(address, message, contextCallback, cancellationToken);

    public Task Forward<T>(Uri address, T message, CancellationToken cancellationToken = default)
        where T : class => context.Forward(address, message, cancellationToken);

    public Task Forward<T>(Uri address, object message, CancellationToken cancellationToken = default)
        where T : class => context.Forward<T>(address, message, cancellationToken);

    public Task RespondAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => context.RespondAsync(message, contextCallback, cancellationToken);

    public Task RespondAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class => context.RespondAsync<T>(message, contextCallback, cancellationToken);
}
