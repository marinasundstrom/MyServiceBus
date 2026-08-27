using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class NServiceBusJsonMessageContext : IMessageContext
{
    private readonly JsonDocument _jsonDocument;
    private readonly Dictionary<Type, object> _messageCache = new();
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public NServiceBusJsonMessageContext(byte[] jsonBytes, IDictionary<string, object> transportHeaders)
    {
        _jsonDocument = JsonDocument.Parse(jsonBytes);
        Headers = new Dictionary<string, object>(transportHeaders);
        MessageId = (ReadNullableGuid(NServiceBusHeaders.MessageId) ?? ReadNullableGuid("message_id")).GetValueOrDefault();
        RequestId = ReadNullableGuid(NServiceBusHeaders.RelatedTo) ?? MessageId;
        CorrelationId = ReadNullableGuid(NServiceBusHeaders.CorrelationId) ?? ReadNullableGuid("correlation_id");
        ConversationId = ReadNullableGuid(NServiceBusHeaders.ConversationId);
        MessageType = ReadMessageTypes();
        ResponseAddress = ReadAddress(NServiceBusHeaders.ReplyToAddress) ?? ReadAddress("reply_to");
        SentTime = ReadSentTime();
    }

    public Guid MessageId { get; }
    public Guid? RequestId { get; }
    public Guid? CorrelationId { get; }
    public Guid? ConversationId { get; }
    public IList<string> MessageType { get; }
    public Uri? ResponseAddress { get; }
    public Uri? FaultAddress => null;
    public IDictionary<string, object> Headers { get; }
    public DateTimeOffset SentTime { get; }
    public string ContentType => InboundMessageResolver.RawJsonContentType;
    public InboundMessageFormat Format => InboundMessageFormat.NServiceBusJson;

    public bool TryGetMessage<T>(out T? message)
        where T : class
    {
        if (_messageCache.TryGetValue(typeof(T), out var cached))
        {
            message = cached as T;
            return message is not null;
        }

        try
        {
            message = _jsonDocument.RootElement.Deserialize<T>(_serializerOptions);
            if (message is not null)
                _messageCache[typeof(T)] = message;
            return message is not null;
        }
        catch (JsonException)
        {
            message = null;
            return false;
        }
    }

    private IList<string> ReadMessageTypes()
    {
        var value = ReadHeader(NServiceBusHeaders.EnclosedMessageTypes);
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ToMessageUrn)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private Guid? ReadNullableGuid(string name)
        => Guid.TryParse(ReadHeader(name), out var value) ? value : null;

    private Uri? ReadAddress(string name)
    {
        var value = ReadHeader(name);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Uri.TryCreate(value, UriKind.Absolute, out var address)
            ? address
            : new Uri($"queue:{value}");
    }

    private DateTimeOffset ReadSentTime()
    {
        var value = ReadHeader(NServiceBusHeaders.TimeSent);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var sentTime)
            ? sentTime
            : default;
    }

    private string? ReadHeader(string name)
    {
        if (!Headers.TryGetValue(name, out var value))
            return null;
        return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString();
    }

    private static string ToMessageUrn(string enclosedType)
    {
        var fullName = enclosedType.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        var separator = fullName.LastIndexOf('.');
        return separator < 0
            ? $"urn:message::{fullName}"
            : $"urn:message:{fullName[..separator]}:{fullName[(separator + 1)..].Replace('+', '.')}";
    }
}
