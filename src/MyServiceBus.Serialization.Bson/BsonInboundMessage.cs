using Newtonsoft.Json;

namespace MyServiceBus.Serialization.Bson;

internal sealed class BsonInboundMessage : IInboundMessage
{
    private readonly JsonSerializerSettings _deserializerSettings;
    private readonly BsonEnvelope _envelope;
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly Dictionary<Type, object> _messageCache = [];
    private readonly IDictionary<string, object> _transportHeaders;
    private Dictionary<string, object>? _headers;

    public BsonInboundMessage(
        BsonEnvelope envelope,
        IDictionary<string, object> transportHeaders,
        JsonSerializerSettings deserializerSettings,
        IMessageHeaderConvention headerConvention)
    {
        _envelope = envelope;
        _transportHeaders = transportHeaders;
        _deserializerSettings = deserializerSettings;
        _headerConvention = headerConvention;
    }

    public Guid MessageId => ParseGuid(_envelope.MessageId).GetValueOrDefault();

    public Guid? RequestId => ParseGuid(_envelope.RequestId);

    public Guid? CorrelationId => ParseGuid(_envelope.CorrelationId);

    public Guid? ConversationId => ParseGuid(_envelope.ConversationId);

    public Guid? InitiatorId => ParseGuid(_envelope.InitiatorId);

    public IList<string> MessageType => _envelope.MessageType;

    public Uri? ResponseAddress => ParseUri(_envelope.ResponseAddress);

    public Uri? FaultAddress
    {
        get
        {
            if (ParseUri(_envelope.FaultAddress) is { } envelopeAddress)
                return envelopeAddress;

            if (!_transportHeaders.TryGetValue(_headerConvention.FaultAddressHeader, out var value))
                return null;

            return value switch
            {
                Uri uri => uri,
                byte[] bytes => ParseUri(System.Text.Encoding.UTF8.GetString(bytes)),
                _ => ParseUri(value?.ToString())
            };
        }
    }

    public IDictionary<string, object> Headers => _headers ??= MergeHeaders();

    public DateTimeOffset SentTime => _envelope.SentTime.GetValueOrDefault();

    public string ContentType => BsonSerializerFactory.BsonContentType;

    public InboundMessageFormat Format => InboundMessageFormat.Envelope;

    public bool TryGetMessage<T>(out T? message) where T : class
    {
        if (_messageCache.TryGetValue(typeof(T), out var cached))
        {
            message = cached as T;
            return message is not null;
        }

        try
        {
            var serializer = Newtonsoft.Json.JsonSerializer.Create(_deserializerSettings);
            message = _envelope.Message?.ToObject<T>(serializer);
            if (message is not null)
                _messageCache[typeof(T)] = message;
            return message is not null;
        }
        catch
        {
            message = null;
            return false;
        }
    }

    private Dictionary<string, object> MergeHeaders()
    {
        var headers = _envelope.Headers.ToDictionary(
            pair => pair.Key,
            pair => pair.Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _transportHeaders)
            headers[pair.Key] = pair.Value;
        return headers;
    }

    private static Guid? ParseGuid(string? value)
        => Guid.TryParse(value, out var result) ? result : null;

    private static Uri? ParseUri(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var result) ? result : null;
}
