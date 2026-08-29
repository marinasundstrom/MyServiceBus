namespace MyServiceBus.Serialization;

public sealed class EnvelopeSerializerFactory : ISerializerFactory
{
    private readonly IMessageHeaderConvention _headerConvention;

    public EnvelopeSerializerFactory()
        : this(MassTransitHeaderConvention.Instance)
    {
    }

    public EnvelopeSerializerFactory(IMessageHeaderConvention headerConvention)
    {
        _headerConvention = headerConvention;
    }

    public string ContentType => InboundMessageResolver.EnvelopeContentType;

    public IMessageSerializer CreateSerializer() => new EnvelopeMessageSerializer(_headerConvention);

    public IMessageDeserializer CreateDeserializer() => new EnvelopeMessageDeserializer(_headerConvention);
}
