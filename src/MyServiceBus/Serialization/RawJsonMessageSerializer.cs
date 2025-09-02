using System.Text.Json;

namespace MyServiceBus.Serialization;

public class RawJsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Throws(typeof(NotSupportedException))]
    public Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context) where T : class
    {
        context.Headers["content_type"] = "application/json";
        return Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(context.Message!, _options));
    }
}
