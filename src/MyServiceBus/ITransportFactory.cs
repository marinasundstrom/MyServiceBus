using MyServiceBus.Topology;

namespace MyServiceBus;

public interface ITransportFactory
{
    [Throws(typeof(InvalidOperationException))]
    Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default);

    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default);
}
