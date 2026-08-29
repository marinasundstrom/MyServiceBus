using System.Text;

namespace MyServiceBus.Serialization;

public sealed class NServiceBusJsonMessageDeserializer : IMessageDeserializer
{
    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Raw;

    public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
        => new NServiceBusJsonMessageContext(body.GetBytes(), headers);

    public MessageBody GetMessageBody(string text)
        => new ByteArrayMessageBody(Encoding.UTF8.GetBytes(text));
}
