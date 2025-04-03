using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyServiceBus;

public class MassTransitEnvelope<TMessage>
    where TMessage : class
{
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }

    [JsonPropertyName("conversationId")]
    public Guid? ConversationId { get; set; }

    [JsonPropertyName("correlationId")]
    public Guid? CorrelationId { get; set; }

    [JsonPropertyName("sourceAddress")]
    public string SourceAddress { get; set; }

    [JsonPropertyName("destinationAddress")]
    public string DestinationAddress { get; set; }

    [JsonPropertyName("messageType")]
    public List<string> MessageType { get; set; }

    [JsonPropertyName("message")]
    public TMessage Message { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, object> Headers { get; set; }

    [JsonPropertyName("host")]
    public HostInfo Host { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; }
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