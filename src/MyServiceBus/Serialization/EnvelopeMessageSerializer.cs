using System.Buffers;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Linq;

namespace MyServiceBus.Serialization;

public class EnvelopeMessageSerializer : IMessageSerializer, IMessageSerializerMetadata
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public EnvelopeMessageSerializer()
        : this(JsonSerializationDefaults.CreateOptions(), MassTransitHeaderConvention.Instance)
    {
    }

    public EnvelopeMessageSerializer(IMessageHeaderConvention headerConvention)
        : this(JsonSerializationDefaults.CreateOptions(), headerConvention)
    {
    }

    public EnvelopeMessageSerializer(
        JsonSerializerOptions jsonSerializerOptions,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => InboundMessageResolver.EnvelopeContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Envelope;

    public MessageBody GetMessageBody<T>(MessageSerializationContext<T> context) where T : class
    {
        context.Headers[_headerConvention.ContentTypeHeader] = ContentType;

        var headers = context.Headers
            .Where(kv => !_headerConvention.IsHostHeader(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = _jsonSerializerOptions.WriteIndented,
            Encoder = _jsonSerializerOptions.Encoder
        }))
        {
            writer.WriteStartObject();
            writer.WriteString("messageId", context.MessageId);
            WriteNullableGuid(writer, "requestId", context.RequestId);
            WriteNullableGuid(writer, "correlationId", context.CorrelationId);
            WriteNullableGuid(writer, "conversationId", context.ConversationId);
            WriteNullableGuid(writer, "initiatorId", context.InitiatorId);
            WriteNullableUri(writer, "sourceAddress", context.SourceAddress);
            WriteNullableUri(writer, "destinationAddress", context.DestinationAddress);
            WriteNullableUri(writer, "responseAddress", context.ResponseAddress);
            WriteNullableUri(writer, "faultAddress", context.FaultAddress);
            writer.WriteNull("expirationTime");
            writer.WriteString("sentTime", context.SentTime);

            writer.WriteStartArray("messageType");
            foreach (var messageType in context.MessageType)
                writer.WriteStringValue(messageType);
            writer.WriteEndArray();

            writer.WritePropertyName("message");
            JsonSerializer.Serialize(writer, context.Message, GetTypeInfo<T>());

            writer.WriteStartObject("headers");
            foreach (var header in headers)
            {
                writer.WritePropertyName(header.Key);
                WriteHeaderValue(writer, header.Value);
            }
            writer.WriteEndObject();

            WriteHost(writer, context.HostInfo);
            writer.WriteString("contentType", "application/json");
            writer.WriteEndObject();
        }

        return new ByteArrayMessageBody(buffer.WrittenMemory.ToArray());
    }

    private static void WriteNullableGuid(Utf8JsonWriter writer, string name, Guid? value)
    {
        if (value.HasValue)
            writer.WriteString(name, value.Value);
        else
            writer.WriteNull(name);
    }

    private static void WriteNullableUri(Utf8JsonWriter writer, string name, Uri? value)
    {
        if (value is not null)
            writer.WriteString(name, value.ToString());
        else
            writer.WriteNull(name);
    }

    private static void WriteHost(Utf8JsonWriter writer, HostInfo? host)
    {
        if (host is null)
        {
            writer.WriteNull("host");
            return;
        }

        writer.WriteStartObject("host");
        writer.WriteString("machineName", host.MachineName);
        writer.WriteString("processName", host.ProcessName);
        writer.WriteNumber("processId", host.ProcessId);
        writer.WriteString("assembly", host.Assembly);
        writer.WriteString("assemblyVersion", host.AssemblyVersion);
        writer.WriteString("frameworkVersion", host.FrameworkVersion);
        writer.WriteString("massTransitVersion", host.MassTransitVersion);
        writer.WriteString("operatingSystemVersion", host.OperatingSystemVersion);
        writer.WriteEndObject();
    }

    private void WriteHeaderValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case byte number:
                writer.WriteNumberValue(number);
                break;
            case short number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case float number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case Guid guid:
                writer.WriteStringValue(guid);
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime);
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset);
                break;
            case Uri uri:
                writer.WriteStringValue(uri.ToString());
                break;
            case byte[] bytes:
                writer.WriteBase64StringValue(bytes);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case IDictionary<string, object> dictionary:
                writer.WriteStartObject();
                foreach (var item in dictionary)
                {
                    writer.WritePropertyName(item.Key);
                    WriteHeaderValue(writer, item.Value);
                }
                writer.WriteEndObject();
                break;
            case IEnumerable values:
                writer.WriteStartArray();
                foreach (var item in values)
                    WriteHeaderValue(writer, item);
                writer.WriteEndArray();
                break;
            default:
                JsonSerializer.Serialize(writer, value, _jsonSerializerOptions.GetTypeInfo(value.GetType()));
                break;
        }
    }

    private JsonTypeInfo<T> GetTypeInfo<T>()
        => _jsonSerializerOptions.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"JSON metadata is not configured for {typeof(T)}.");
}
