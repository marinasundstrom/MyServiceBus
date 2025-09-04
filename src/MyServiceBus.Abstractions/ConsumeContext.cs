
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

    Task Forward<T>(Uri address, T message, CancellationToken cancellationToken = default) where T : class;

    Task Forward<T>(Uri address, object message, CancellationToken cancellationToken = default) where T : class;
}

public interface ConsumeContext<out TMessage> : ConsumeContext
    where TMessage : class
{
    TMessage Message { get; }
}