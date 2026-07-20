using System.Text.Json.Serialization;

namespace MyServiceBus;

public sealed class TransportCapabilityDescriptor
{
    public TransportCapabilityDescriptor(
        string transport,
        IReadOnlyDictionary<string, TransportCapabilitySupport> capabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);
        ArgumentNullException.ThrowIfNull(capabilities);

        Transport = transport;
        Capabilities = capabilities;
    }

    [JsonPropertyName("version")]
    public int Version => 1;

    [JsonPropertyName("transport")]
    public string Transport { get; }

    [JsonPropertyName("capabilities")]
    public IReadOnlyDictionary<string, TransportCapabilitySupport> Capabilities { get; }

    public TransportCapabilitySupport Get(string capability) =>
        Capabilities.TryGetValue(capability, out var support)
            ? support
            : TransportCapabilitySupport.Unsupported;
}

public static class TransportCapabilities
{
    public const string DirectedSend = "directedSend";
    public const string PublishSubscribe = "publishSubscribe";
    public const string Durability = "durability";
    public const string CompetingConsumers = "competingConsumers";
    public const string Acknowledgement = "acknowledgement";
    public const string RequestResponse = "requestResponse";
    public const string Scheduling = "scheduling";
    public const string Redelivery = "redelivery";
    public const string ErrorDestinations = "errorDestinations";
    public const string Ordering = "ordering";
    public const string Replay = "replay";
    public const string TemporaryEndpoints = "temporaryEndpoints";
    public const string TopologyProvisioning = "topologyProvisioning";
}

public static class TransportCapabilityDescriptors
{
    public static TransportCapabilityDescriptor Unknown(string transport) =>
        new(transport, new Dictionary<string, TransportCapabilitySupport>());

    public static TransportCapabilityDescriptor RabbitMq { get; } = new(
        "rabbitmq",
        new Dictionary<string, TransportCapabilitySupport>
        {
            [TransportCapabilities.DirectedSend] = TransportCapabilitySupport.Native,
            [TransportCapabilities.PublishSubscribe] = TransportCapabilitySupport.Native,
            [TransportCapabilities.Durability] = TransportCapabilitySupport.Native,
            [TransportCapabilities.CompetingConsumers] = TransportCapabilitySupport.Native,
            [TransportCapabilities.Acknowledgement] = TransportCapabilitySupport.Native,
            [TransportCapabilities.RequestResponse] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.Scheduling] = TransportCapabilitySupport.Unsupported,
            [TransportCapabilities.Redelivery] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.ErrorDestinations] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.Ordering] = TransportCapabilitySupport.Native,
            [TransportCapabilities.Replay] = TransportCapabilitySupport.Unsupported,
            [TransportCapabilities.TemporaryEndpoints] = TransportCapabilitySupport.Native,
            [TransportCapabilities.TopologyProvisioning] = TransportCapabilitySupport.Native
        });

    public static TransportCapabilityDescriptor InMemory { get; } = new(
        "in-memory",
        new Dictionary<string, TransportCapabilitySupport>
        {
            [TransportCapabilities.DirectedSend] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.PublishSubscribe] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.Durability] = TransportCapabilitySupport.Unsupported,
            [TransportCapabilities.CompetingConsumers] = TransportCapabilitySupport.Unsupported,
            [TransportCapabilities.Acknowledgement] = TransportCapabilitySupport.Unsupported,
            [TransportCapabilities.RequestResponse] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.Scheduling] = TransportCapabilitySupport.Unsupported,
            [TransportCapabilities.Redelivery] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.ErrorDestinations] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.Ordering] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.Replay] = TransportCapabilitySupport.Unsupported,
            [TransportCapabilities.TemporaryEndpoints] = TransportCapabilitySupport.Emulated,
            [TransportCapabilities.TopologyProvisioning] = TransportCapabilitySupport.Unsupported
        });
}
