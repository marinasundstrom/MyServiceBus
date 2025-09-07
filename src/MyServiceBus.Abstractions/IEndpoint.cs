using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IEndpoint
{
    Task Send<T>(T message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Envelope<object>> ReadAsync(CancellationToken cancellationToken = default);

    EndpointCapabilities Capabilities { get; }
}

