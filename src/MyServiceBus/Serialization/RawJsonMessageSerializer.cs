using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MyServiceBus.Serialization;

public class RawJsonMessageSerializer : IMessageSerializer, IMessageSerializerMetadata
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public RawJsonMessageSerializer()
        : this(JsonSerializationDefaults.CreateOptions(), MassTransitHeaderConvention.Instance)
    {
    }

    public RawJsonMessageSerializer(IMessageHeaderConvention headerConvention)
        : this(JsonSerializationDefaults.CreateOptions(), headerConvention)
    {
    }

    public RawJsonMessageSerializer(
        JsonSerializerOptions jsonSerializerOptions,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Raw;

    public MessageBody GetMessageBody<T>(MessageSerializationContext<T> context) where T : class
    {
        context.Headers[_headerConvention.ContentTypeHeader] = ContentType;
        var typeInfo = _jsonSerializerOptions.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"JSON metadata is not configured for {typeof(T)}.");
        return new ByteArrayMessageBody(JsonSerializer.SerializeToUtf8Bytes(context.Message!, typeInfo));
    }
}
