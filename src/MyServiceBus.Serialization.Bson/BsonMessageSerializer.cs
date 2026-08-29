using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace MyServiceBus.Serialization.Bson;

public sealed class BsonMessageSerializer : IMessageSerializer, IMessageSerializerMetadata
{
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly JsonSerializerSettings _serializerSettings;

    public BsonMessageSerializer()
        : this(BsonSerializationSettings.Create(), MassTransitHeaderConvention.Instance)
    {
    }

    public BsonMessageSerializer(
        JsonSerializerSettings serializerSettings,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(serializerSettings);
        _serializerSettings = serializerSettings;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public string ContentType => BsonSerializerFactory.BsonContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Envelope;

    public MessageBody GetMessageBody<T>(MessageSerializationContext<T> context) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            context.Headers[_headerConvention.ContentTypeHeader] = ContentType;
            var serializer = Newtonsoft.Json.JsonSerializer.Create(_serializerSettings);
            var envelope = new BsonEnvelope
            {
                MessageId = Format(context.MessageId),
                RequestId = Format(context.RequestId),
                CorrelationId = Format(context.CorrelationId),
                ConversationId = Format(context.ConversationId),
                InitiatorId = Format(context.InitiatorId),
                SourceAddress = context.SourceAddress?.ToString(),
                DestinationAddress = context.DestinationAddress?.ToString(),
                ResponseAddress = context.ResponseAddress?.ToString(),
                FaultAddress = context.FaultAddress?.ToString(),
                SentTime = context.SentTime,
                MessageType = context.MessageType,
                Message = JToken.FromObject(context.Message, serializer),
                Headers = context.Headers
                    .Where(pair => !_headerConvention.IsHostHeader(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.OrdinalIgnoreCase),
                Host = context.HostInfo
            };

            using var stream = new MemoryStream();
            using (var writer = new BsonDataWriter(stream))
            {
                serializer.Serialize(writer, envelope, typeof(BsonEnvelope));
                writer.Flush();
            }

            return new ByteArrayMessageBody(stream.ToArray());
        }
        catch (BsonSerializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new BsonSerializationException(
                $"Failed to serialize {typeof(T)} using the MassTransit BSON envelope.",
                exception);
        }
    }

    private static string? Format(Guid value) => value == Guid.Empty ? null : value.ToString();

    private static string? Format(Guid? value) => value?.ToString();
}
