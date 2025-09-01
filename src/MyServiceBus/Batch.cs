namespace MyServiceBus;

/// <summary>
/// Represents a collection of messages that should be delivered together as a
/// single message payload. The batch itself is serialized as a JSON array so
/// that the envelope <c>message</c> property contains the grouped messages
/// directly, matching MassTransit batch semantics.
/// </summary>
/// <typeparam name="T">The message type contained in the batch.</typeparam>
public class Batch<T> : List<T>
    where T : class
{
    public Batch()
    {
    }

    public Batch(IEnumerable<T> messages)
        : base(messages)
    {
    }

    public Batch(params T[] messages)
        : base(messages)
    {
    }
}
