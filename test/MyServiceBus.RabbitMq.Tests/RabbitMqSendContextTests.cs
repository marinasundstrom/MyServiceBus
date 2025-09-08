using System;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Serialization;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqSendContextTests
{
    class TestMessage { }

    [Fact]
    [Throws(typeof(IOException))]
    public async Task Uses_context_properties()
    {
        var channel = Substitute.For<IChannel>();
        var transport = new RabbitMqSendTransport(channel, "exchange");
        var serializer = new EnvelopeMessageSerializer();
        var context = new RabbitMqSendContext(MessageTypeCache.GetMessageTypes(typeof(TestMessage)), serializer)
        {
            RoutingKey = "key"
        };
        context.Properties.ContentType = "text/plain";

        await transport.Send(new TestMessage(), context);

        await channel.Received().BasicPublishAsync("exchange", "key", false, context.Properties, Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Throws(typeof(IOException))]
    public async Task Queue_settings_flow_through_context()
    {
        var channel = Substitute.For<IChannel>();
        var transport = new RabbitMqQueueSendTransport(channel, "queue");
        var serializer = new EnvelopeMessageSerializer();
        var context = new RabbitMqSendContext(MessageTypeCache.GetMessageTypes(typeof(TestMessage)), serializer);
        context.TimeToLive = TimeSpan.FromSeconds(5);
        context.Persistent = false;
        context.BrokerProperties["x-priority"] = 5;

        await transport.Send(new TestMessage(), context);

        await channel.Received().BasicPublishAsync(
            string.Empty,
            "queue",
            false,
            Arg.Is<BasicProperties>([Throws(typeof(KeyNotFoundException))] (p) => p.Expiration == ((long)TimeSpan.FromSeconds(5).TotalMilliseconds).ToString() &&
                p.Persistent == false &&
                (int)p.Headers!["x-priority"] == 5),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }
}
