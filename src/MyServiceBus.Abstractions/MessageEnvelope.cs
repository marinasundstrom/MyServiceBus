using System.Text.Json.Serialization;

namespace MyServiceBus;

public class Envelope<TMessage>
    where TMessage : class
{
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }

    [JsonPropertyName("requestId")]
    public Guid? RequestId { get; set; }

    [JsonPropertyName("correlationId")]
    public Guid? CorrelationId { get; set; }

    [JsonPropertyName("conversationId")]
    public Guid? ConversationId { get; set; }

    [JsonPropertyName("initiatorId")]
    public Guid? InitiatorId { get; set; }

    [JsonPropertyName("sourceAddress")]
    public Uri? SourceAddress { get; set; }

    [JsonPropertyName("destinationAddress")]
    public Uri? DestinationAddress { get; set; }

    [JsonPropertyName("responseAddress")]
    public Uri? ResponseAddress { get; set; }

    [JsonPropertyName("faultAddress")]
    public Uri? FaultAddress { get; set; }

    [JsonPropertyName("expirationTime")]
    public DateTimeOffset? ExpirationTime { get; set; }

    [JsonPropertyName("sentTime")]
    public DateTimeOffset? SentTime { get; set; }

    [JsonPropertyName("messageType")]
    public List<string> MessageType { get; set; } = new();

    [JsonPropertyName("message")]
    public TMessage Message { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, object> Headers { get; set; } = new();

    [JsonPropertyName("host")]
    public HostInfo? Host { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }
}

public class HostInfo
{
    [JsonPropertyName("machineName")]
    public string MachineName { get; set; }

    [JsonPropertyName("processName")]
    public string ProcessName { get; set; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    [JsonPropertyName("assembly")]
    public string Assembly { get; set; }

    [JsonPropertyName("assemblyVersion")]
    public string AssemblyVersion { get; set; }

    [JsonPropertyName("frameworkVersion")]
    public string FrameworkVersion { get; set; }

    [JsonPropertyName("massTransitVersion")]
    public string MassTransitVersion { get; set; }

    [JsonPropertyName("operatingSystemVersion")]
    public string OperatingSystemVersion { get; set; }
}