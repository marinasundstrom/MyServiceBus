using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class DefaultConsumeContext<TMessage> : BasePipeContext, ConsumeContext<TMessage>
    where TMessage : class
{
    public DefaultConsumeContext(TMessage message, CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        Message = message;
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

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        throw new NotImplementedException();
    }
}
