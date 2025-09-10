
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface ConsumeContext :
    PipeContext,
    MessageConsumeContext,
    IPublishEndpoint,
    ISendEndpointProvider
{

    Task Send<T>(Uri address, T message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class;

    Task Send<T>(Uri address, object message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class;

    async Task ScheduleSend<T>(Uri address, TimeSpan delay, T message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var endpoint = await GetSendEndpoint(address).ConfigureAwait(false);
        await endpoint.ScheduleSend(delay, message, contextCallback, cancellationToken).ConfigureAwait(false);
    }

    Task ScheduleSend<T>(Uri address, DateTime scheduledTime, T message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(address, scheduledTime - DateTime.UtcNow, message, contextCallback, cancellationToken);

    async Task ScheduleSend<T>(Uri address, TimeSpan delay, object message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var endpoint = await GetSendEndpoint(address).ConfigureAwait(false);
        await endpoint.ScheduleSend<T>(delay, message, contextCallback, cancellationToken).ConfigureAwait(false);
    }

    Task ScheduleSend<T>(Uri address, DateTime scheduledTime, object message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class
        => ScheduleSend<T>(address, scheduledTime - DateTime.UtcNow, message, contextCallback, cancellationToken);

    Task Forward<T>(Uri address, T message, CancellationToken cancellationToken = default) where T : class;

    Task Forward<T>(Uri address, object message, CancellationToken cancellationToken = default) where T : class;
}

public interface ConsumeContext<out TMessage> : ConsumeContext
    where TMessage : class
{
    TMessage Message { get; }
}