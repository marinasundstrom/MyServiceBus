using System.Text;
using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class EnvelopeMessageDeserializer : IMessageDeserializer
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public EnvelopeMessageDeserializer()
        : this(JsonSerializationDefaults.CreateOptions(), MassTransitHeaderConvention.Instance)
    {
    }

    public EnvelopeMessageDeserializer(IMessageHeaderConvention headerConvention)
        : this(JsonSerializationDefaults.CreateOptions(), headerConvention)
    {
    }

    public EnvelopeMessageDeserializer(
        JsonSerializerOptions jsonSerializerOptions,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => InboundMessageResolver.EnvelopeContentType;

    public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
        => new EnvelopeMessageContext(body.GetBytes(), headers, _jsonSerializerOptions, _headerConvention);

    public MessageBody GetMessageBody(string text)
        => new ByteArrayMessageBody(Encoding.UTF8.GetBytes(text));
}
