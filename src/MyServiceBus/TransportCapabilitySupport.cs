using System.Text.Json.Serialization;

namespace MyServiceBus;

[JsonConverter(typeof(JsonStringEnumConverter<TransportCapabilitySupport>))]
public enum TransportCapabilitySupport
{
    [JsonStringEnumMemberName("native")]
    Native,
    [JsonStringEnumMemberName("emulated")]
    Emulated,
    [JsonStringEnumMemberName("unsupported")]
    Unsupported
}
