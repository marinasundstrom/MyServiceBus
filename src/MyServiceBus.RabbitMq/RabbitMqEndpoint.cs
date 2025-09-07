using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class RabbitMqEndpoint : IEndpoint
{
    private readonly ISendEndpoint _sendEndpoint;

    public RabbitMqEndpoint(ISendEndpoint sendEndpoint)
    {
        _sendEndpoint = sendEndpoint;
    }

    public EndpointCapabilities Capabilities =>
        EndpointCapabilities.Acknowledgement | EndpointCapabilities.Retry | EndpointCapabilities.BatchSend;

    public Task Send<T>(T message, CancellationToken cancellationToken = default)
        => _sendEndpoint.Send(message, null, cancellationToken);

    public IAsyncEnumerable<Envelope<object>> ReadAsync(CancellationToken cancellationToken = default)
        => AsyncEnumerable.Empty<Envelope<object>>();
}

