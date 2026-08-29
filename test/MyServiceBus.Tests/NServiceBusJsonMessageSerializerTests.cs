using System.Text;
using MyServiceBus.Serialization;

namespace MyServiceBus.Tests;

public class NServiceBusJsonMessageSerializerTests
{
    [NServiceBusMessageType("Contracts.SubmitOrder")]
    private sealed class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    [Fact]
    public void Serializes_plain_json_with_nservicebus_metadata()
    {
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var context = new MessageSerializationContext<TestMessage>(new TestMessage { Text = "hi" })
        {
            MessageId = messageId,
            ConversationId = conversationId,
            Headers = new Dictionary<string, object>(),
            SentTime = DateTimeOffset.UtcNow,
            Intent = MessageIntent.Publish
        };

        var body = new NServiceBusJsonMessageSerializer().GetMessageBody(context).GetBytes();

        Assert.Equal("{\"Text\":\"hi\"}", Encoding.UTF8.GetString(body));
        Assert.Equal("application/json", context.Headers[NServiceBusHeaders.ContentType]);
        Assert.Equal("Contracts.SubmitOrder", context.Headers[NServiceBusHeaders.EnclosedMessageTypes]);
        Assert.Equal(messageId.ToString(), context.Headers[NServiceBusHeaders.MessageId]);
        Assert.Equal(conversationId.ToString(), context.Headers[NServiceBusHeaders.ConversationId]);
        Assert.Equal("Publish", context.Headers[NServiceBusHeaders.MessageIntent]);
        Assert.Equal("application/json", context.Headers["_content_type"]);
        Assert.Equal(messageId.ToString(), context.Headers["_message_id"]);
    }

    [Fact]
    public void Resolves_nservicebus_json_and_metadata_separately_from_raw_json()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var headers = new Dictionary<string, object>
        {
            [NServiceBusHeaders.ContentType] = Encoding.UTF8.GetBytes("application/json"),
            [NServiceBusHeaders.EnclosedMessageTypes] = Encoding.UTF8.GetBytes("Contracts.SubmitOrder, Contracts"),
            [NServiceBusHeaders.MessageId] = messageId.ToString(),
            [NServiceBusHeaders.CorrelationId] = correlationId.ToString(),
            [NServiceBusHeaders.ReplyToAddress] = "replies"
        };

        var inbound = new InboundMessageResolver().Resolve(
            new StubTransportMessage(Encoding.UTF8.GetBytes("{\"text\":\"hi\"}"), headers));

        Assert.Equal(InboundMessageFormat.NServiceBusJson, inbound.Format);
        Assert.Equal(messageId, inbound.MessageId);
        Assert.Equal(messageId, inbound.RequestId);
        Assert.Equal(correlationId, inbound.CorrelationId);
        Assert.Equal("urn:message:Contracts:SubmitOrder", Assert.Single(inbound.MessageType));
        Assert.Equal(new Uri("queue:replies"), inbound.ResponseAddress);
        Assert.True(inbound.TryGetMessage<TestMessage>(out var message));
        Assert.Equal("hi", message!.Text);
    }

    private sealed record StubTransportMessage(byte[] Payload, IDictionary<string, object> Headers)
        : Transports.ITransportMessage
    {
        public bool IsDurable => true;
    }
}
