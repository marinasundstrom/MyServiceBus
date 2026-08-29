using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace MyServiceBus.Serialization.Bson;

public sealed class BsonMessageDeserializer : IMessageDeserializer
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerSettings _deserializerSettings;

    public BsonMessageDeserializer()
        : this(BsonSerializationSettings.Create(), MassTransitHeaderConvention.Instance)
    {
    }

    public BsonMessageDeserializer(
        JsonSerializerSettings deserializerSettings,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(deserializerSettings);
        _deserializerSettings = deserializerSettings;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => BsonSerializerFactory.BsonContentType;

    public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(headers);

        try
        {
            using var stream = body.GetStream();
            using var reader = new BsonDataReader(stream);
            var serializer = Newtonsoft.Json.JsonSerializer.Create(_deserializerSettings);
            var envelope = serializer.Deserialize<BsonEnvelope>(reader)
                ?? throw new InvalidDataException("The MassTransit BSON envelope was not found.");
            return new BsonInboundMessage(envelope, headers, _deserializerSettings, _headerConvention);
        }
        catch (BsonSerializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new BsonSerializationException(
                "Failed to deserialize the MassTransit BSON envelope.",
                exception);
        }
    }

    public MessageBody GetMessageBody(string text)
    {
        try
        {
            return new ByteArrayMessageBody(Convert.FromBase64String(text));
        }
        catch (Exception exception)
        {
            throw new BsonSerializationException("The BSON message body is not valid Base64.", exception);
        }
    }
}
