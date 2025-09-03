using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class DefaultConsumeContext<TMessage> : BasePipeContext, ConsumeContext<TMessage>
    where TMessage : class
{
    readonly ISendEndpointProvider? sendEndpointProvider;

    public DefaultConsumeContext(TMessage message, ISendEndpointProvider? sendEndpointProvider = null, CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        Message = message;
        this.sendEndpointProvider = sendEndpointProvider;
    }

    public TMessage Message { get; }

    public Task RespondAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    [Throws(typeof(InvalidOperationException))]
    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        if (sendEndpointProvider == null)
            throw new InvalidOperationException("SendEndpointProvider not configured");

        return sendEndpointProvider.GetSendEndpoint(uri);
    }
}
