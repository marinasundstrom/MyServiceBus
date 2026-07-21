using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface MessageConsumeContext
{
    Guid? RequestId => null;
    Guid? CorrelationId => null;
    Guid? ConversationId => null;
    Guid? InitiatorId => null;
    IDictionary<string, object> Headers => new Dictionary<string, object>();

    Task RespondAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class;

    Task RespondAsync<T>(object message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class;
}
