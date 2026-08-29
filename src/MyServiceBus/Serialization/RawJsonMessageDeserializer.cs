using System.Text;
using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class RawJsonMessageDeserializer : IMessageDeserializer
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public RawJsonMessageDeserializer()
        : this(JsonSerializationDefaults.CreateOptions(), MassTransitHeaderConvention.Instance)
    {
    }

    public RawJsonMessageDeserializer(IMessageHeaderConvention headerConvention)
        : this(JsonSerializationDefaults.CreateOptions(), headerConvention)
    {
    }

    public RawJsonMessageDeserializer(
        JsonSerializerOptions jsonSerializerOptions,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
        => new RawJsonMessageContext(body.GetBytes(), headers, _jsonSerializerOptions, _headerConvention);

    public MessageBody GetMessageBody(string text)
        => new ByteArrayMessageBody(Encoding.UTF8.GetBytes(text));
}
