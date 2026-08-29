namespace MyServiceBus.Serialization;

public interface IMessageDeserializer
{
    string ContentType { get; }

    MessageEnvelopeMode EnvelopeMode { get; }

    IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers);

    MessageBody GetMessageBody(string text);
}
