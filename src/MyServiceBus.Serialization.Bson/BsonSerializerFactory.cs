using Newtonsoft.Json;

namespace MyServiceBus.Serialization.Bson;

public sealed class BsonSerializerFactory : ISerializerFactory
{
    public const string BsonContentType = "application/vnd.masstransit+bson";

    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerSettings _serializerSettings;
    private readonly JsonSerializerSettings _deserializerSettings;

    public BsonSerializerFactory()
        : this(BsonSerializationSettings.Create(), BsonSerializationSettings.Create())
    {
    }

    public BsonSerializerFactory(JsonSerializerSettings settings)
        : this(settings, settings)
    {
    }

    public BsonSerializerFactory(
        JsonSerializerSettings serializerSettings,
        JsonSerializerSettings deserializerSettings,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(serializerSettings);
        ArgumentNullException.ThrowIfNull(deserializerSettings);
        _serializerSettings = serializerSettings;
        _deserializerSettings = deserializerSettings;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => BsonContentType;

    public IMessageSerializer CreateSerializer()
        => new BsonMessageSerializer(_serializerSettings, _headerConvention);

    public IMessageDeserializer CreateDeserializer()
        => new BsonMessageDeserializer(_deserializerSettings, _headerConvention);
}
