namespace MyServiceBus.Serialization;

public class MessageSerializationContext<T>
    where T : class
{
    public MessageSerializationContext(T message)
    {
        Message = message;
    }

    public Guid MessageId { get; set; }

    public Guid? CorrelationId { get; set; }

    public IList<string> MessageType { get; set; }

    public Uri? ResponseAddress { get; set; }

    public Uri? FaultAddress { get; set; }

    public Uri? SourceAddress { get; set; }

    public Uri? DestinationAddress { get; set; }

    public IDictionary<string, object> Headers { get; set; }

    public DateTimeOffset SentTime { get; set; }

    public T Message { get; set; }

    public HostInfo HostInfo { get; set; }
}
