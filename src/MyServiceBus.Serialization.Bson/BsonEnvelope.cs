using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyServiceBus.Serialization.Bson;

internal sealed class BsonEnvelope
{
    [JsonProperty("messageId")]
    public string? MessageId { get; set; }

    [JsonProperty("requestId")]
    public string? RequestId { get; set; }

    [JsonProperty("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonProperty("conversationId")]
    public string? ConversationId { get; set; }

    [JsonProperty("initiatorId")]
    public string? InitiatorId { get; set; }

    [JsonProperty("sourceAddress")]
    public string? SourceAddress { get; set; }

    [JsonProperty("destinationAddress")]
    public string? DestinationAddress { get; set; }

    [JsonProperty("responseAddress")]
    public string? ResponseAddress { get; set; }

    [JsonProperty("faultAddress")]
    public string? FaultAddress { get; set; }

    [JsonProperty("expirationTime")]
    public DateTimeOffset? ExpirationTime { get; set; }

    [JsonProperty("sentTime")]
    public DateTimeOffset? SentTime { get; set; }

    [JsonProperty("messageType")]
    public IList<string> MessageType { get; set; } = [];

    [JsonProperty("message")]
    public JToken? Message { get; set; }

    [JsonProperty("headers")]
    public Dictionary<string, object?> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonProperty("host")]
    public HostInfo? Host { get; set; }
}
