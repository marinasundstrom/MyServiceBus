using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class EnvelopeSerializerFactory : ISerializerFactory
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public EnvelopeSerializerFactory()
        : this(JsonSerializationDefaults.CreateOptions(), MassTransitHeaderConvention.Instance)
    {
    }

    public EnvelopeSerializerFactory(IMessageHeaderConvention headerConvention)
        : this(JsonSerializationDefaults.CreateOptions(), headerConvention)
    {
    }

    public EnvelopeSerializerFactory(
        JsonSerializerOptions jsonSerializerOptions,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => InboundMessageResolver.EnvelopeContentType;

    public IMessageSerializer CreateSerializer() => new EnvelopeMessageSerializer(_jsonSerializerOptions, _headerConvention);

    public IMessageDeserializer CreateDeserializer() => new EnvelopeMessageDeserializer(_jsonSerializerOptions, _headerConvention);
}
