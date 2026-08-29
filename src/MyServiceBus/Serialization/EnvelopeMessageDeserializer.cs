using System.Text;

namespace MyServiceBus.Serialization;

public sealed class EnvelopeMessageDeserializer : IMessageDeserializer
{
    private readonly IMessageHeaderConvention _headerConvention;

    public EnvelopeMessageDeserializer()
        : this(MassTransitHeaderConvention.Instance)
    {
    }

    public EnvelopeMessageDeserializer(IMessageHeaderConvention headerConvention)
    {
        _headerConvention = headerConvention;
    }

    public string ContentType => InboundMessageResolver.EnvelopeContentType;

    public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
        => new EnvelopeMessageContext(body.GetBytes(), headers, _headerConvention);

    public MessageBody GetMessageBody(string text)
        => new ByteArrayMessageBody(Encoding.UTF8.GetBytes(text));
}
