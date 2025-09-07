using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IEndpoint
{
    Task Send<T>(T message, Action<ISendContext>? configure = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ConsumeContext> ReadAsync(CancellationToken cancellationToken = default);

    IDisposable Subscribe(Func<ConsumeContext, Task> handler);

    EndpointCapabilities Capabilities { get; }
}

