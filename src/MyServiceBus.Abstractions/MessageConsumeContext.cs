using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface MessageConsumeContext
{
    Task RespondAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default);
}
