using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MyServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqSendContextTests
{
    class TestMessage { }

    [Fact]
    public async Task RoutingKey_is_applied_from_context()
    {
        var channel = Substitute.For<IChannel>();
        string? capturedKey = null;
        channel.BasicPublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask)
            .AndDoes(ci => capturedKey = ci.ArgAt<string>(1));

        var transport = new RabbitMqSendTransport(channel, "test");
        var context = new RabbitMqSendContext(MessageTypeCache.GetMessageTypes(typeof(TestMessage)), new EnvelopeMessageSerializer())
        {
            RoutingKey = "order"
        };

        await transport.Send(new TestMessage(), context);

        capturedKey.ShouldBe("order");
    }
}

