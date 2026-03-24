using System.Net.Mime;
using System.Text.Json;

namespace MyServiceBus.Serialization;

public class RawJsonMessageContext : IMessageContext
{
    private readonly JsonDocument _jsonDocument;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Dictionary<Type, object> _messageCache = new();
    private readonly IDictionary<string, object> _transportHeaders;
    private readonly IMessageHeaderConvention _headerConvention;

    public RawJsonMessageContext(byte[] jsonBytes, IDictionary<string, object> transportHeaders, IMessageHeaderConvention? headerConvention = null)
    {
        _jsonDocument = JsonDocument.Parse(jsonBytes);
        _transportHeaders = transportHeaders;
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;

        // These will just be "empty" in raw mode
        MessageId = Guid.Empty;
        CorrelationId = null;
        Headers = new Dictionary<string, object>(transportHeaders);
        MessageType = new List<string>();
        SentTime = DateTime.UtcNow;

        if (transportHeaders.TryGetValue(_headerConvention.FaultAddressHeader, out var header))
        {
            if (header is string str)
                FaultAddress = new Uri(str);
            else if (header is Uri uri)
                FaultAddress = uri;
        }
    }

    public Guid MessageId { get; }
    public Guid? CorrelationId { get; }
    public IDictionary<string, object> Headers { get; }
    public IList<string> MessageType { get; }
    public Uri? ResponseAddress { get; }
    public Uri? FaultAddress { get; }
    public DateTimeOffset SentTime { get; }
    public string ContentType => InboundMessageResolver.RawJsonContentType;
    public InboundMessageFormat Format => InboundMessageFormat.RawJson;

    public bool TryGetMessage<T>(out T? message) where T : class
    {
        if (_messageCache.TryGetValue(typeof(T), out var cached))
        {
            message = cached as T;
            return message != null;
        }

        try
        {
            message = _jsonDocument.RootElement.Deserialize<T>(_jsonSerializerOptions);
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
}
