namespace MyServiceBus.Serialization;

public interface IMessageContext
{
    Guid MessageId { get; }
    Guid? CorrelationId { get; }
    IList<string> MessageType { get; }
    Uri? ResponseAddress { get; }
    Uri? FaultAddress { get; }

    IDictionary<string, object> Headers { get; }

    DateTimeOffset SentTime { get; }

    [Throws(typeof(ObjectDisposedException))]
    bool TryGetMessage<T>(out T? message)
        where T : class;
}
