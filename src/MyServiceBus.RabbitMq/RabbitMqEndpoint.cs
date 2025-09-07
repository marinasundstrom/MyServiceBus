using System.Collections.Generic;
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

    public async IAsyncEnumerable<Envelope<object>> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

