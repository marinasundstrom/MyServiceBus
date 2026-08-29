using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MyServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus.RabbitMq.Tests;

public class TransportHeaderTests
{
    class TestMessage { }

    [Fact]
    public async Task Underscore_headers_are_applied_to_basic_properties()
    {
        var channel = Substitute.For<IChannel>();
        BasicProperties? captured = null;
        channel.BasicPublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask)
            .AndDoes(ci => captured = ci.Arg<BasicProperties>());

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

    [Fact]
    public async Task Mandatory_transport_requires_exchange_routing()
    {
        var channel = Substitute.For<IChannel>();
        channel.BasicPublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        var transport = new RabbitMqSendTransport(channel, "input_error", mandatory: true);
        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(TestMessage)), new EnvelopeMessageSerializer())
        {
            RoutingKey = string.Empty
        };

        await transport.Send(new TestMessage(), context);

        await channel.Received(1).BasicPublishAsync(
            "input_error",
            string.Empty,
            true,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }
}
