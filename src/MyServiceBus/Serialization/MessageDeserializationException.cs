namespace MyServiceBus.Serialization;

/// <summary>A supported message payload could not be materialized for consumption.</summary>
public sealed class MessageDeserializationException : Exception
{
    public MessageDeserializationException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
