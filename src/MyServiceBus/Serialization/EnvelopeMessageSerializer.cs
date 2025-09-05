using System.Text.Json;
using System.Linq;

namespace MyServiceBus.Serialization;

public class EnvelopeMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Throws(typeof(NotSupportedException), typeof(ArgumentException))]
    public Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context) where T : class
    {
        context.Headers["content_type"] = "application/vnd.masstransit+json";

        var headers = context.Headers
            .Where(kv => !kv.Key.StartsWith("MT-Host-", StringComparison.Ordinal))
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
