using System.Text.Json;

namespace MyServiceBus.Serialization;

public class EnvelopeMessageContext : IMessageContext
{
    private readonly JsonDocument _jsonDocument;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Dictionary<Type, object> _messageCache = new();
    private readonly IDictionary<string, object> _transportHeaders;
    private readonly IMessageHeaderConvention _headerConvention;

    private Guid? _messageId;
    private Guid? _correlationId;
    private Guid? _requestId;
    private Guid? _conversationId;
    private Guid? _initiatorId;
    private List<string>? _messageType;
    private Dictionary<string, object>? _headers;
    private DateTimeOffset? _sentTime;
    private Uri? _responseAddress;
    private Uri? _faultAddress;

    public EnvelopeMessageContext(byte[] jsonBytes, IDictionary<string, object> transportHeaders, IMessageHeaderConvention? headerConvention = null)
    {
        _jsonDocument = JsonDocument.Parse(jsonBytes);
        _transportHeaders = transportHeaders;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public Guid MessageId =>
        (_messageId ??= TryGetGuidProperty("messageId")).GetValueOrDefault();

    public Guid? CorrelationId =>
        _correlationId ??= TryGetGuidProperty("correlationId");

    public Guid? RequestId =>
        _requestId ??= TryGetGuidProperty("requestId");

    public Guid? ConversationId =>
        _conversationId ??= TryGetGuidProperty("conversationId");

    public Guid? InitiatorId =>
        _initiatorId ??= TryGetGuidProperty("initiatorId");

    public IList<string> MessageType =>
        _messageType ??= TryGetProperty("messageType")?.Deserialize<List<string>>() ?? new();

    public Uri? ResponseAddress
    {
        get
        {
            if (_responseAddress is not null)
            {
                return _responseAddress;
            }
            var s = TryGetProperty("responseAddress")?.GetString();
            if (s is not null)
            {
                _responseAddress = new Uri(s);
            }
            return _responseAddress;
        }
    }

    public Uri? FaultAddress
    {
        get
        {
            if (_faultAddress is not null)
            {
                return _faultAddress;
            }
            var s = TryGetProperty("faultAddress")?.GetString();
            if (s is null && _transportHeaders.TryGetValue(_headerConvention.FaultAddressHeader, out var header))
            {
                if (header is string str)
                    s = str;
                else if (header is Uri uri)
                    _faultAddress = uri;
            }

            if (s is not null)
                _faultAddress = new Uri(s);

            return _faultAddress;
        }
    }

    public IDictionary<string, object> Headers => _headers ??= MergeHeaders();

    public DateTimeOffset SentTime =>
        _sentTime ??= TryGetProperty("sentTime")?.GetDateTimeOffset() ?? default;

    public string ContentType => InboundMessageResolver.EnvelopeContentType;

    public InboundMessageFormat Format => InboundMessageFormat.Envelope;

    public bool TryGetMessage<T>(out T? message) where T : class
    {
        if (_messageCache.TryGetValue(typeof(T), out var cached))
        {
            message = cached as T;
            return message != null;
        }

        if (!_jsonDocument.RootElement.TryGetProperty("message", out var value))
        {
            message = null;
            return false;
        }

        try
        {
            message = value.Deserialize<T>(_jsonSerializerOptions);
            if (message != null)
                _messageCache[typeof(T)] = message;
            return message != null;
        }
        catch
        {
            message = null;
            return false;
        }
    }

    private JsonElement? TryGetProperty(string propertyName)
    {
        return _jsonDocument.RootElement.TryGetProperty(propertyName, out var value) ? value : null;
    }

    private Guid? TryGetGuidProperty(string propertyName)
    {
        var value = TryGetProperty(propertyName);
        return value is { ValueKind: JsonValueKind.String } && value.Value.TryGetGuid(out var parsed)
            ? parsed
            : null;
    }

    private Dictionary<string, object> MergeHeaders()
    {
        var envelopeHeaders = TryGetProperty("headers")?.Deserialize<Dictionary<string, object>>() ?? new();
        foreach (var kv in _transportHeaders)
        {
            envelopeHeaders[kv.Key] = kv.Value;
        }
        return envelopeHeaders;
    }
}
