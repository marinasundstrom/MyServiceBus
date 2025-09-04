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

    public Task PublishAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        return Task.CompletedTask;
    }

    public Task PublishAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        return Task.CompletedTask;
    }

    [Throws(typeof(InvalidCastException))]
    public Task Forward<T>(Uri address, T message, CancellationToken cancellationToken = default) where T : class
        => Forward<T>(address, (object)message!, cancellationToken);

    [Throws(typeof(InvalidOperationException))]
    public async Task Forward<T>(Uri address, object message, CancellationToken cancellationToken = default) where T : class
    {
        if (sendEndpointProvider == null)
            throw new InvalidOperationException("SendEndpointProvider not configured");

        var endpoint = await sendEndpointProvider.GetSendEndpoint(address).ConfigureAwait(false);
        await endpoint.Send<T>(message, null, cancellationToken).ConfigureAwait(false);
    }

    [Throws(typeof(InvalidOperationException))]
    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        if (sendEndpointProvider == null)
            throw new InvalidOperationException("SendEndpointProvider not configured");

        return sendEndpointProvider.GetSendEndpoint(uri);
    }
}
