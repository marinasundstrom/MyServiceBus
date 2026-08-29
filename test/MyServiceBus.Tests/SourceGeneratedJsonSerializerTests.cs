using System.Text.Json;
using System.Text.Json.Serialization;
using MyServiceBus.Serialization;

namespace MyServiceBus.Tests;

public class SourceGeneratedJsonSerializerTests
{
    [Fact]
    public void Envelope_factory_uses_application_generated_metadata_on_send_and_receive()
    {
        var factory = new EnvelopeSerializerFactory(SourceGeneratedJsonContext.Default.Options);
        var context = CreateContext(new SourceGeneratedMessage { Text = "generated" });
        context.Headers["attempt"] = 3;

        var body = factory.CreateSerializer().GetMessageBody(context);
        var inbound = factory.CreateDeserializer().Deserialize(body, new Dictionary<string, object>());

        Assert.True(inbound.TryGetMessage<SourceGeneratedMessage>(out var message));
        Assert.Equal("generated", message!.Text);
        Assert.Equal(JsonValueKind.Number, Assert.IsType<JsonElement>(inbound.Headers["attempt"]).ValueKind);
    }

    [Fact]
    public void Raw_factory_uses_application_generated_metadata_on_send_and_receive()
    {
        var factory = new RawJsonSerializerFactory(SourceGeneratedJsonContext.Default.Options);
        var context = CreateContext(new SourceGeneratedMessage { Text = "generated" });

        var body = factory.CreateSerializer().GetMessageBody(context);
        var inbound = factory.CreateDeserializer().Deserialize(body, context.Headers);

        Assert.True(inbound.TryGetMessage<SourceGeneratedMessage>(out var message));
        Assert.Equal("generated", message!.Text);
    }

    private static MessageSerializationContext<T> CreateContext<T>(T message)
        where T : class
        => new(message)
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            MessageType = [MessageUrn.For(typeof(T))],
            Headers = new Dictionary<string, object>(),
            SentTime = DateTimeOffset.UtcNow,
            HostInfo = new HostInfo
            {
                MachineName = "test",
                ProcessName = "test",
                Assembly = "test",
                AssemblyVersion = "1.0.0",
                FrameworkVersion = ".NET",
                MassTransitVersion = "1.0.0",
                OperatingSystemVersion = "test"
            }
        };
}

public sealed class SourceGeneratedMessage
{
    public string Text { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SourceGeneratedMessage))]
internal partial class SourceGeneratedJsonContext : JsonSerializerContext
{
}
