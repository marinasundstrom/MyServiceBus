namespace MyServiceBus.Serialization.Bson;

/// <summary>
/// The MassTransit BSON envelope could not be serialized or deserialized.
/// </summary>
public sealed class BsonSerializationException : Exception
{
    public BsonSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
