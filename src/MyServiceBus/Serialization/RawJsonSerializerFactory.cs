using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class RawJsonSerializerFactory : ISerializerFactory
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public RawJsonSerializerFactory()
        : this(JsonSerializationDefaults.CreateOptions(), MassTransitHeaderConvention.Instance)
    {
    }

    public RawJsonSerializerFactory(IMessageHeaderConvention headerConvention)
        : this(JsonSerializationDefaults.CreateOptions(), headerConvention)
    {
    }

    public RawJsonSerializerFactory(
        JsonSerializerOptions jsonSerializerOptions,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public IMessageSerializer CreateSerializer() => new RawJsonMessageSerializer(_jsonSerializerOptions, _headerConvention);

    public IMessageDeserializer CreateDeserializer() => new RawJsonMessageDeserializer(_jsonSerializerOptions, _headerConvention);
}
