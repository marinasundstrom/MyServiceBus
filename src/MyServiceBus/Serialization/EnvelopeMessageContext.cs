using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MyServiceBus.Serialization;

public class EnvelopeMessageContext : IMessageContext
{
    private readonly JsonDocument _jsonDocument;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
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

    public EnvelopeMessageContext(
        byte[] jsonBytes,
        IDictionary<string, object> transportHeaders,
        IMessageHeaderConvention? headerConvention = null)
        : this(jsonBytes, transportHeaders, JsonSerializationDefaults.CreateOptions(), headerConvention)
    {
    }

    public EnvelopeMessageContext(
        byte[] jsonBytes,
        IDictionary<string, object> transportHeaders,
        JsonSerializerOptions jsonSerializerOptions,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        try { _jsonDocument = JsonDocument.Parse(jsonBytes); }
        catch (JsonException exception) { throw new MessageDeserializationException("Cannot parse message envelope.", exception); }
        if (_jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
        {
            _jsonDocument.Dispose();
            throw new MessageDeserializationException("A message envelope must be a JSON object.");
        }
        _jsonSerializerOptions = jsonSerializerOptions;
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
        _messageType ??= ReadMessageTypes();

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

    /// <exception cref="MessageDeserializationException">The payload is missing, null, or invalid for the requested contract.</exception>
    public bool TryGetMessage<T>(out T? message) where T : class
    {
        if (_messageCache.TryGetValue(typeof(T), out var cached))
        {
            message = cached as T;
            return message != null;
        }

        if (!_jsonDocument.RootElement.TryGetProperty("message", out var value))
        {
            throw new MessageDeserializationException("The envelope has no message body.");
        }

        try
        {
            var typeInfo = _jsonSerializerOptions.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
                ?? throw new InvalidOperationException($"JSON metadata is not configured for {typeof(T)}.");
            message = value.Deserialize(typeInfo);
            if (message is null)
                throw new MessageDeserializationException($"The payload for {typeof(T)} is null.");
            _messageCache[typeof(T)] = message;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new MessageDeserializationException($"Cannot deserialize message as {typeof(T)}.", exception);
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
        var envelopeHeaders = new Dictionary<string, object>();
        if (TryGetProperty("headers") is { ValueKind: JsonValueKind.Object } headers)
        {
            foreach (var property in headers.EnumerateObject())
                envelopeHeaders[property.Name] = property.Value.Clone();
        }
        foreach (var kv in _transportHeaders)
        {
            envelopeHeaders[kv.Key] = kv.Value;
        }
        return envelopeHeaders;
    }

    private List<string> ReadMessageTypes()
    {
        var messageTypes = new List<string>();
        if (TryGetProperty("messageType") is not { ValueKind: JsonValueKind.Array } values)
            return messageTypes;

        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String && value.GetString() is { } messageType)
                messageTypes.Add(messageType);
        }
        return messageTypes;
    }
}
