using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public sealed class EndpointReceiveTransport : IReceiveTransport
{
    private readonly IEndpoint _endpoint;
    private readonly Func<ReceiveContext, Task> _handler;
    private CancellationTokenSource? _cts;
    private Task? _pump;

    public EndpointReceiveTransport(IEndpoint endpoint, Func<ReceiveContext, Task> handler)
    {
        _endpoint = endpoint;
        _handler = handler;
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pump = Task.Run(async () =>
        {
            await foreach (var context in _endpoint.ReadAsync(_cts.Token))
                await _handler(context);
        }, _cts.Token);
        return Task.CompletedTask;
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        if (_cts == null)
            return;

        _cts.Cancel();
        if (_pump != null)
            await _pump;
    }
}
