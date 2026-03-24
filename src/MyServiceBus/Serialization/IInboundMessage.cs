namespace MyServiceBus.Serialization;

public interface IInboundMessage
{
    Guid MessageId { get; }
    Guid? CorrelationId { get; }
    IList<string> MessageType { get; }
    Uri? ResponseAddress { get; }
    Uri? FaultAddress { get; }
    IDictionary<string, object> Headers { get; }
    DateTimeOffset SentTime { get; }
    string ContentType { get; }
    InboundMessageFormat Format { get; }

    bool TryGetMessage<T>(out T? message)
        where T : class;
}
