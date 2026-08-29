using MyServiceBus.Serialization;
using MyServiceBus.Serialization.Bson;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Shouldly;

namespace MyServiceBus.Tests;

public class BsonSerializerTests
{
    [Fact]
    public void MassTransit_reads_the_MyServiceBus_BSON_envelope()
    {
        var body = new BsonSerializerFactory().CreateSerializer().GetMessageBody(CreateContext());

        using var stream = body.GetStream();
        using var reader = new BsonDataReader(stream);
        var envelope = MassTransit.Serialization.BsonMessageSerializer.Deserializer
            .Deserialize<MassTransit.Serialization.MessageEnvelope>(reader);

        envelope.ShouldNotBeNull();
        envelope.MessageId.ShouldBe("124f4bc4-bc2f-45a7-bf9a-ddeba5aab587");
        envelope.CorrelationId.ShouldBe("cf46535d-f7d4-451d-857f-9c64b64339da");
        var token = envelope.Message as JToken;
        token.ShouldNotBeNull();
        var message = token.ToObject<BsonTestMessage>();
        message.ShouldNotBeNull();
        message.OrderId.ShouldBe(Guid.Parse("f8f53c23-1fbb-4f18-970d-3a6d27fd9c19"));
        message.Total.ShouldBe(1234.56m);
    }

    [Fact]
    public void MyServiceBus_reads_the_MassTransit_BSON_envelope()
    {
        var envelope = new MassTransit.Serialization.JsonMessageEnvelope
        {
            MessageId = "124f4bc4-bc2f-45a7-bf9a-ddeba5aab587",
            CorrelationId = "cf46535d-f7d4-451d-857f-9c64b64339da",
            ConversationId = "c7bba23f-49a4-40c4-869d-20e36a0dd38c",
            SentTime = DateTime.Parse("2026-08-29T12:34:56.123456Z").ToUniversalTime(),
            MessageType = ["urn:message:MyServiceBus.Tests:BsonTestMessage"],
            Message = new BsonTestMessage
            {
                OrderId = Guid.Parse("f8f53c23-1fbb-4f18-970d-3a6d27fd9c19"),
                Total = 1234.56m
            },
            Headers = new Dictionary<string, object?> { ["attempt"] = 2 }
        };
        using var stream = new MemoryStream();
        using (var writer = new BsonDataWriter(stream))
        {
            MassTransit.Serialization.BsonMessageSerializer.Serializer
                .Serialize(writer, envelope, typeof(MassTransit.Serialization.MessageEnvelope));
            writer.Flush();
        }

        var inbound = new BsonSerializerFactory()
            .CreateDeserializer()
            .Deserialize(new ByteArrayMessageBody(stream.ToArray()), new Dictionary<string, object>());

        inbound.CorrelationId.ShouldBe(Guid.Parse("cf46535d-f7d4-451d-857f-9c64b64339da"));
        inbound.TryGetMessage<BsonTestMessage>(out var message).ShouldBeTrue();
        message.ShouldNotBeNull();
        message.OrderId.ShouldBe(Guid.Parse("f8f53c23-1fbb-4f18-970d-3a6d27fd9c19"));
        message.Total.ShouldBe(1234.56m);
        Convert.ToInt32(inbound.Headers["attempt"]).ShouldBe(2);
    }

    [Fact]
    public void Reads_the_Java_BSON_fixture()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "serialization-fixtures",
            "java-bson-envelope.base64");
        var body = new ByteArrayMessageBody(Convert.FromBase64String(File.ReadAllText(fixturePath).Trim()));

        var inbound = new BsonSerializerFactory()
            .CreateDeserializer()
            .Deserialize(body, new Dictionary<string, object>());

        inbound.CorrelationId.ShouldBe(Guid.Parse("cf46535d-f7d4-451d-857f-9c64b64339da"));
        inbound.TryGetMessage<BsonTestMessage>(out var message).ShouldBeTrue();
        message.ShouldNotBeNull();
        message.OrderId.ShouldBe(Guid.Parse("f8f53c23-1fbb-4f18-970d-3a6d27fd9c19"));
        message.Total.ShouldBe(1234.56m);
        Convert.ToInt32(inbound.Headers["attempt"]).ShouldBe(2);
    }

    [Fact]
    public void Factory_round_trips_the_MassTransit_envelope()
    {
        var factory = new BsonSerializerFactory();
        var serializer = factory.CreateSerializer();
        var deserializer = factory.CreateDeserializer();
        var context = CreateContext();

        var body = serializer.GetMessageBody(context);
        var inbound = deserializer.Deserialize(body, new Dictionary<string, object>
        {
            ["transport"] = "rabbitmq"
        });

        serializer.ContentType.ShouldBe(BsonSerializerFactory.BsonContentType);
        context.Headers[MassTransitHeaderConvention.Instance.ContentTypeHeader]
            .ShouldBe(BsonSerializerFactory.BsonContentType);
        inbound.ContentType.ShouldBe(BsonSerializerFactory.BsonContentType);
        inbound.Format.ShouldBe(InboundMessageFormat.Envelope);
        inbound.MessageId.ShouldBe(context.MessageId);
        inbound.CorrelationId.ShouldBe(context.CorrelationId);
        inbound.ConversationId.ShouldBe(context.ConversationId);
        inbound.MessageType.ShouldBe(context.MessageType);
        inbound.Headers["transport"].ShouldBe("rabbitmq");
        Convert.ToInt32(inbound.Headers["attempt"]).ShouldBe(2);
        inbound.TryGetMessage<BsonTestMessage>(out var message).ShouldBeTrue();
        message.ShouldNotBeNull();
        message.OrderId.ShouldBe(context.Message.OrderId);
        message.Total.ShouldBe(1234.56m);
    }

    [Fact]
    public void Text_body_uses_the_MassTransit_Base64_convention()
    {
        var factory = new BsonSerializerFactory();
        var context = CreateContext();
        var body = factory.CreateSerializer().GetMessageBody(context);
        var text = Convert.ToBase64String(body.GetBytes());

        var restored = factory.CreateDeserializer().GetMessageBody(text);

        restored.GetBytes().ShouldBe(body.GetBytes());
    }

    private static MessageSerializationContext<BsonTestMessage> CreateContext()
        => new(new BsonTestMessage
        {
            OrderId = Guid.Parse("f8f53c23-1fbb-4f18-970d-3a6d27fd9c19"),
            Total = 1234.56m
        })
        {
            MessageId = Guid.Parse("124f4bc4-bc2f-45a7-bf9a-ddeba5aab587"),
            CorrelationId = Guid.Parse("cf46535d-f7d4-451d-857f-9c64b64339da"),
            ConversationId = Guid.Parse("c7bba23f-49a4-40c4-869d-20e36a0dd38c"),
            MessageType = [MessageUrn.For(typeof(BsonTestMessage))],
            Headers = new Dictionary<string, object> { ["attempt"] = 2 },
            SentTime = DateTimeOffset.Parse("2026-08-29T12:34:56.123456Z"),
            HostInfo = new HostInfo
            {
                MachineName = "test",
                ProcessName = "test",
                ProcessId = 42,
                Assembly = "MyServiceBus.Tests",
                AssemblyVersion = "1.0.0",
                FrameworkVersion = ".NET",
                MassTransitVersion = "1.0.0",
                OperatingSystemVersion = "test"
            }
        };

    public sealed class BsonTestMessage
    {
        public Guid OrderId { get; set; }

        public decimal Total { get; set; }
    }
}
