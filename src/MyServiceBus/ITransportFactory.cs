using MyServiceBus.Topology;

namespace MyServiceBus;

public interface ITransportFactory
{
    TransportCapabilityDescriptor Capabilities => TransportCapabilityDescriptors.Unknown(GetType().Name);

    Uri GetPublishAddress(string entityName) => new($"exchange:{entityName}");

    Uri GetTemporaryEndpointAddress(string endpointName) =>
        new($"exchange:{endpointName}?durable=false&autodelete=true");

    Uri GetErrorAddress(string endpointName) => GetPublishAddress(endpointName + "_error");

    Uri GetFaultAddress(string endpointName) => GetPublishAddress(endpointName + "_fault");

    Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default);

    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default);

    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTransportTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        var binding = topology.Bindings.Single();
        return CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = topology.Name,
                ExchangeName = binding.EntityName,
                RoutingKey = string.Empty,
                ExchangeType = "fanout",
                Durable = topology.Durable,
                AutoDelete = topology.Temporary,
                PrefetchCount = topology.PrefetchCount,
                QueueArguments = topology.TransportOptions is null
                    ? null
                    : new Dictionary<string, object?>(topology.TransportOptions, StringComparer.Ordinal)
            },
            handler,
            isMessageTypeRegistered,
            cancellationToken);
    }
}
