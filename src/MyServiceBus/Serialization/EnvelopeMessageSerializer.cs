using System.Text.Json;

namespace MyServiceBus.Serialization;

public class EnvelopeMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Throws(typeof(NotSupportedException))]
    public Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context) where T : class
    {
        var messageType = typeof(T);
        var envelope = new Envelope<T>()
        {
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            ConversationId = null,
            //SourceAddress = new Uri("rabbitmq://localhost/source"),
            //DestinationAddress = new Uri("rabbitmq://localhost/source" + messageType.Name),
            ResponseAddress = context.ResponseAddress,
            FaultAddress = context.FaultAddress,
            MessageType = (List<string>)context.MessageType,
            Message = context.Message!,
            SentTime = context.SentTime,
            Headers = (Dictionary<string, object>)context.Headers,
            Host = context.HostInfo,
            ContentType = "application/json"
        };
        return Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(envelope, _options));
    }
}
