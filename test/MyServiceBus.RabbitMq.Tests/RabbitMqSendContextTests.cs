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
}
