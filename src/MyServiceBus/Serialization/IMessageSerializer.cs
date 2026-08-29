namespace MyServiceBus.Serialization;

public interface IMessageSerializer
{
    string ContentType { get; }

    MessageEnvelopeMode EnvelopeMode { get; }

    MessageBody GetMessageBody<T>(MessageSerializationContext<T> context)
        where T : class;
}
