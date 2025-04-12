namespace MyServiceBus.Serialization;

public interface IMessageContext
{
    Guid MessageId { get; }
    Guid? CorrelationId { get; }
    IList<string> MessageType { get; }
    IDictionary<string, object> Headers { get; }
    public DateTimeOffset SentTime { get; }

    [Throws(typeof(InvalidOperationException), typeof(ObjectDisposedException))]
    bool TryGetMessage<T>(out T? message)
        where T : class;
}
