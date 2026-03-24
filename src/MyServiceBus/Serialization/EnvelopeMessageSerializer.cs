using System.Text.Json;
using System.Linq;

namespace MyServiceBus.Serialization;

public class EnvelopeMessageSerializer : IMessageSerializer
{
    private readonly IMessageHeaderConvention _headerConvention;

    public EnvelopeMessageSerializer()
        : this(MassTransitHeaderConvention.Instance)
    {
    }

    public EnvelopeMessageSerializer(IMessageHeaderConvention headerConvention)
    {
        _headerConvention = headerConvention;
    }

    public string ContentType => InboundMessageResolver.EnvelopeContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Envelope;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context) where T : class
    {
        context.Headers[_headerConvention.ContentTypeHeader] = ContentType;

        var headers = context.Headers
            .Where(kv => !_headerConvention.IsHostHeader(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var envelope = new Envelope<T>()
        {
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            ConversationId = null,
            SourceAddress = context.SourceAddress,
            DestinationAddress = context.DestinationAddress,
            ResponseAddress = context.ResponseAddress,
            FaultAddress = context.FaultAddress,
            MessageType = (List<string>)context.MessageType,
            Message = context.Message!,
            SentTime = context.SentTime,
            Headers = headers,
            Host = context.HostInfo,
            ContentType = "application/json"
        };
        return Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(envelope, _options));
    }
}
