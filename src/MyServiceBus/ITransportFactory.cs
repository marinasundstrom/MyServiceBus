using MyServiceBus.Topology;

namespace MyServiceBus;

public interface ITransportFactory
{
    TransportCapabilityDescriptor Capabilities => TransportCapabilityDescriptors.Unknown(GetType().Name);

    Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default);

    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default);
}
