using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IEndpoint
{
    Task Send<T>(T message, Action<ISendContext>? configure = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ReceiveContext> ReadAsync(CancellationToken cancellationToken = default);

    IDisposable Subscribe(Func<ReceiveContext, Task> handler);

    EndpointCapabilities Capabilities { get; }
}

