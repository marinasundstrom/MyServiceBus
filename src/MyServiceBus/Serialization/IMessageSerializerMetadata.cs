namespace MyServiceBus.Serialization;

/// <summary>
/// Optional MyServiceBus metadata for serializers whose wire format needs
/// dispatch behavior beyond the MassTransit-compatible serializer contract.
/// </summary>
public interface IMessageSerializerMetadata
{
    MessageEnvelopeMode EnvelopeMode { get; }
}
