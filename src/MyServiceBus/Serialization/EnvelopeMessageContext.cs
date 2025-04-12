using System.Text.Json;

namespace MyServiceBus.Serialization;

public class EnvelopeMessageContext : IMessageContext
{
    private readonly JsonDocument _jsonDocument;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Dictionary<Type, object> _messageCache = new();

    private Guid? _messageId;
    private Guid? _correlationId;
    private List<string>? _messageType;
    private Dictionary<string, object>? _headers;
    private DateTimeOffset? _sentTime;

    public EnvelopeMessageContext(byte[] jsonBytes, IDictionary<string, object> transportHeaders)
    {
        _jsonDocument = JsonDocument.Parse(jsonBytes);
    }

    public Guid MessageId =>
        (_messageId ??= TryGetProperty("messageId")?.GetGuid()).GetValueOrDefault();

    public Guid? CorrelationId =>
        _correlationId ??= TryGetProperty("correlationId")?.GetGuid();

    public IList<string> MessageType =>
        _messageType ??= TryGetProperty("messageType")?.Deserialize<List<string>>() ?? new();

    public IDictionary<string, object> Headers =>
        _headers ??= TryGetProperty("headers")?.Deserialize<Dictionary<string, object>>() ?? new();

    public DateTimeOffset SentTime =>
        _sentTime ??= TryGetProperty("sentTime")?.GetDateTimeOffset() ?? default;

    [Throws(typeof(InvalidOperationException), typeof(ObjectDisposedException))]
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

    [Throws(typeof(InvalidOperationException), typeof(ObjectDisposedException))]
    private JsonElement? TryGetProperty(string propertyName)
    {
        return _jsonDocument.RootElement.TryGetProperty(propertyName, out var value) ? value : null;
    }
}