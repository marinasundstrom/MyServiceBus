namespace MyServiceBus.Serialization;

public sealed class NServiceBusJsonSerializerFactory : ISerializerFactory
{
    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public IMessageSerializer CreateSerializer() => new NServiceBusJsonMessageSerializer();

    public IMessageDeserializer CreateDeserializer() => new NServiceBusJsonMessageDeserializer();
}
