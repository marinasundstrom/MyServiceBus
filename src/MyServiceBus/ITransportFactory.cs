using MyServiceBus.Topology;

namespace MyServiceBus;

public interface ITransportFactory
{
    Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default);
    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        IMessageHandler handler,
        CancellationToken cancellationToken = default);
}
