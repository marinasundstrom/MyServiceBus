using MyServiceBus.Topology;

namespace MyServiceBus;

public interface ITransportFactory
{
    TransportCapabilityDescriptor Capabilities => TransportCapabilityDescriptors.Unknown(GetType().Name);

    Uri GetPublishAddress(string entityName) => new($"exchange:{entityName}");

    Uri GetTemporaryEndpointAddress(string endpointName) =>
        new($"exchange:{endpointName}?durable=false&autodelete=true");

    Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default);

    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default);
}
