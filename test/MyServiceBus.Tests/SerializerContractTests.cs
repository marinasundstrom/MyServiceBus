using System.Text;
using MyServiceBus.Serialization;

namespace MyServiceBus.Tests;

public class SerializerContractTests
{
    private sealed class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    [Fact]
    public void Envelope_factory_creates_matching_serializer_and_deserializer()
    {
        var factory = new EnvelopeSerializerFactory();
        var serializer = factory.CreateSerializer();
        var deserializer = factory.CreateDeserializer();

        Assert.Equal(factory.ContentType, serializer.ContentType);
        Assert.Equal(factory.ContentType, deserializer.ContentType);

        var context = CreateContext(new TestMessage { Text = "hello" });
        var body = serializer.GetMessageBody(context);
        var inbound = deserializer.Deserialize(body, new Dictionary<string, object>());

        Assert.True(inbound.TryGetMessage<TestMessage>(out var message));
        Assert.Equal("hello", message!.Text);
    }

    [Fact]
    public void Raw_json_factory_creates_matching_serializer_and_deserializer()
    {
        var factory = new RawJsonSerializerFactory();
        var serializer = factory.CreateSerializer();
        var deserializer = factory.CreateDeserializer();
        var context = CreateContext(new TestMessage { Text = "hello" });
        var body = serializer.GetMessageBody(context);
        var inbound = deserializer.Deserialize(body, context.Headers);

        Assert.Equal(factory.ContentType, serializer.ContentType);
        Assert.Equal(factory.ContentType, deserializer.ContentType);
        Assert.True(inbound.TryGetMessage<TestMessage>(out var message));
        Assert.Equal("hello", message!.Text);
    }

    [Fact]
    public void Json_deserializer_converts_text_to_a_message_body()
    {
        var deserializer = new RawJsonMessageDeserializer();

        var body = deserializer.GetMessageBody("{\"text\":\"hello\"}");

        Assert.Equal("{\"text\":\"hello\"}", Encoding.UTF8.GetString(body.GetBytes()));
    }

    private static MessageSerializationContext<T> CreateContext<T>(T message) where T : class
        => new(message)
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            MessageType = [MessageUrn.For(typeof(T))],
            Headers = new Dictionary<string, object>(),
            SentTime = DateTimeOffset.UtcNow,
            HostInfo = new HostInfo()
        };
}
