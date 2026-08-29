namespace MyServiceBus.Serialization;

public interface ISerializerFactory
{
    string ContentType { get; }

    IMessageSerializer CreateSerializer();

    IMessageDeserializer CreateDeserializer();
}
