using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class NServiceBusJsonSerializerFactory : ISerializerFactory
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public NServiceBusJsonSerializerFactory()
        : this(JsonSerializationDefaults.CreateNServiceBusOptions())
    {
    }

    public NServiceBusJsonSerializerFactory(JsonSerializerOptions jsonSerializerOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public IMessageSerializer CreateSerializer() => new NServiceBusJsonMessageSerializer(_jsonSerializerOptions);

    public IMessageDeserializer CreateDeserializer() => new NServiceBusJsonMessageDeserializer(_jsonSerializerOptions);
}
