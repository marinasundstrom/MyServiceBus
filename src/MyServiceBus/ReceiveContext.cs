using System.Text.Json;

namespace MyServiceBus;

public class ReceiveContext
{
    public byte[] Body { get; }
    public IDictionary<string, object>? Headers { get; }

    public string? MessageType => Headers.TryGetValue("message-type", out var value) ? value?.ToString() : null;
    public string? CorrelationId => Headers.TryGetValue("correlation-id", out var value) ? value?.ToString() : null;

    public IMessageSerializer Serializer { get; set; } = new JsonMessageSerializer();

    public ReceiveContext(byte[] body, IDictionary<string, object>? headers = null)
    {
        Body = body;
        Headers = headers;
    }

    public T Deserialize<T>()
    {
        return Serializer.Deserialize<T>(Body);
    }

    public object Deserialize(Type type)
    {
        return Serializer.Deserialize(type, Body);
    }
}

internal class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public T Deserialize<T>(byte[] body)
    {
        return JsonSerializer.Deserialize<T>(body, _options);
    }

    public object Deserialize(Type type, byte[] body)
    {
        return JsonSerializer.Deserialize(body, type, _options)!;
    }

    public byte[] Serialize<T>(T? message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }

    /*

    public byte[] Serialize<T>(T message, SendContext context)
    {
        var envelope = new Envelope<T>
        {
            MessageType = typeof(T).FullName!,
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            Headers = context.Headers,
            Message = message!
        };

        return JsonSerializer.SerializeToUtf8Bytes<T>(envelope, _options);
    }

    public Envelope<T> Deserialize<T>(byte[] messageData, ReceiveContext context)
    {
        return JsonSerializer.Deserialize<Envelope<T>>(messageData, _options);
    }

    */
}

public interface IMessageSerializer
{
    T Deserialize<T>(byte[] body);
    object Deserialize(Type type, byte[] body);
    byte[] Serialize<T>(T? message);
}