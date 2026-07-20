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

    /// <summary>
    /// Creates a receive transport from the legacy broker-shaped topology contract.
    /// New transport implementations should override the <see cref="CreateReceiveTransport(ReceiveEndpointTransportTopology, Func{ReceiveContext, Task}, Func{string, bool}, CancellationToken)"/>
    /// overload instead.
    /// </summary>
    [Obsolete("Override CreateReceiveTransport(ReceiveEndpointTransportTopology, ...) for new transport implementations.")]
    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            $"Transport factory '{GetType().Name}' does not implement the legacy receive-topology contract.");

    /// <summary>
    /// Creates a receive transport from profile-neutral endpoint intent.
    /// This is the supported extension point for new transport implementations.
    /// </summary>
    Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTransportTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        var binding = topology.Bindings.Single();
#pragma warning disable CS0618 // Compatibility adapter for legacy transport implementations.
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
#pragma warning restore CS0618
    }
}
