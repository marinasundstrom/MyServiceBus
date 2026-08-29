using System.Text;
using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class NServiceBusJsonMessageDeserializer : IMessageDeserializer
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public NServiceBusJsonMessageDeserializer()
        : this(JsonSerializationDefaults.CreateNServiceBusOptions())
    {
    }

    public NServiceBusJsonMessageDeserializer(JsonSerializerOptions jsonSerializerOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
        => new NServiceBusJsonMessageContext(body.GetBytes(), headers, _jsonSerializerOptions);

    public MessageBody GetMessageBody(string text)
        => new ByteArrayMessageBody(Encoding.UTF8.GetBytes(text));
}
