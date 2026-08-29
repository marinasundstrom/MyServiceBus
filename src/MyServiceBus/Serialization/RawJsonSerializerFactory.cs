namespace MyServiceBus.Serialization;

public sealed class RawJsonSerializerFactory : ISerializerFactory
{
    private readonly IMessageHeaderConvention _headerConvention;

    public RawJsonSerializerFactory()
        : this(MassTransitHeaderConvention.Instance)
    {
    }

    public RawJsonSerializerFactory(IMessageHeaderConvention headerConvention)
    {
        _headerConvention = headerConvention;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public IMessageSerializer CreateSerializer() => new RawJsonMessageSerializer(_headerConvention);

    public IMessageDeserializer CreateDeserializer() => new RawJsonMessageDeserializer(_headerConvention);
}
