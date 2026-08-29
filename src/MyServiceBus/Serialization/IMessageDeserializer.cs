namespace MyServiceBus.Serialization;

public interface IMessageDeserializer
{
    string ContentType { get; }

    IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers);

    MessageBody GetMessageBody(string text);
}
