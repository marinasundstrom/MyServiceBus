using System.Text.Json;

namespace MyServiceBus;

public interface ReceiveContext
{
    public Guid MessageId { get; }
    public string ContentType { get; }
    public byte[] Body { get; }

    T Deserialize<T>()
        where T : class;
    object Deserialize(Type type);

    public string MessageType { get; }
}

public class ReceiveContextImpl : ReceiveContext
{
    public byte[] Body { get; }

    public IDictionary<string, object>? Headers { get; }

    public Guid MessageId => Headers.TryGetValue("message-id", out var value) ? Guid.Parse(value.ToString()) : Guid.Empty;
    public string ContentType => Headers.TryGetValue("content_type", out var value) ? value?.ToString() : null;


    public string? MessageType => Headers.TryGetValue("message-type", out var value) ? value?.ToString() : null;
    public string? CorrelationId => Headers.TryGetValue("correlation-id", out var value) ? value?.ToString() : null;

    public IMessageSerializer Serializer { get; set; } = new JsonMessageSerializer();

    public ReceiveContextImpl(byte[] body, IDictionary<string, object>? headers = null)
    {
        Body = body;
        Headers = headers;
    }

    public T Deserialize<T>()
        where T : class
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
        where T : class
    {
        return JsonSerializer.Deserialize<T>(body, _options);
    }

    public object Deserialize(Type type, byte[] body)
    {
        var type2 = typeof(Envelope<>).MakeGenericType(type);
        dynamic message = JsonSerializer.Deserialize(body, type2, _options)!;
        return message.Message;
    }

    public byte[] Serialize<T>(T? message)
        where T : class
    {
        var messageType = typeof(T);

        var envelope = new Envelope<T>()
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = null,
            ConversationId = Guid.NewGuid(),
            SourceAddress = new Uri("rabbitmq://localhost/source"),
            DestinationAddress = new Uri("rabbitmq://localhost/source" + messageType.Name),
            MessageType = [$"urn:message:{messageType.Namespace}:{messageType.Name}"],
            Message = message!,
            SentTime = DateTimeOffset.UtcNow,
            Headers = {
                //{ "correlation-id", "" },
                { "message-type", NamingConventions.GetMessageName(messageType)}
            },
            Host = new HostInfo
            {
                MachineName = Environment.MachineName,
                ProcessName = Environment.ProcessPath ?? "unknown",
                ProcessId = Environment.ProcessId,
                Assembly = typeof(T).Assembly.GetName().Name ?? "unknown",
                AssemblyVersion = typeof(T).Assembly.GetName().Version?.ToString() ?? "unknown",
                FrameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                MassTransitVersion = "your-custom-version", // replace as needed
                OperatingSystemVersion = Environment.OSVersion.VersionString
            },
            ContentType = "application/json"
        };
        return JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
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
    T Deserialize<T>(byte[] body)
        where T : class;
    object Deserialize(Type type, byte[] body);

    byte[] Serialize<T>(T? message)
        where T : class;
}