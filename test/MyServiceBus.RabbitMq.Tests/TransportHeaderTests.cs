using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MyServiceBus.Serialization;

namespace MyServiceBus.RabbitMq.Tests;

public class TransportHeaderTests
{
    class TestMessage { }

    [Fact]
    public async Task Underscore_headers_are_applied_to_basic_properties()
    {
        var channel = Substitute.For<IChannel>();
        IBasicProperties? captured = null;
        channel.CreateBasicProperties().Returns(new BasicProperties());
        channel.BasicPublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<IBasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => captured = ci.Arg<IBasicProperties>());

        var transport = new RabbitMqSendTransport(channel, "test");
        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(TestMessage)), new EnvelopeMessageSerializer())
        {
            RoutingKey = string.Empty
        };
        context.Headers["_correlation_id"] = "123";

        await transport.Send(new TestMessage(), context);

        captured.ShouldNotBeNull();
        captured!.CorrelationId.ShouldBe("123");
        (captured.Headers == null || !captured.Headers.ContainsKey("correlation_id")).ShouldBeTrue();
    }
}

