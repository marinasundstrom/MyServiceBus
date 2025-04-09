namespace MyServiceBus;

public class SendContext
{
    public string MessageId { get; set; }
    public string RoutingKey { get; set; } = ""; // Defaults to empty
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>();
    public string? CorrelationId { get; set; }
    public bool Persistent { get; set; } = true;

    // Optional serializer for this message
    public IMessageSerializer Serializer { get; set; } = new JsonMessageSerializer();

    public ReadOnlyMemory<byte> Serialize<T>(T message)
        where T : class
    {
        return Serializer.Serialize(message);
    }
}