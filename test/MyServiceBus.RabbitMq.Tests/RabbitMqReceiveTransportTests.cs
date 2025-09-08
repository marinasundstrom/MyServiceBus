using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqReceiveTransportTests
{
    [Fact]
    [Throws(typeof(ArrayTypeMismatchException), typeof(EncoderFallbackException))]
    public async Task Acks_message_when_handler_fails()
    {
        var channel = Substitute.For<IChannel>();
        AsyncEventingBasicConsumer? consumer = null;

        channel
            .BasicConsumeAsync(
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IDictionary<string, object>>(),
                Arg.Any<IAsyncBasicConsumer>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                consumer = (AsyncEventingBasicConsumer)ci[6]!;
                return Task.FromResult("tag");
            });

        var transport = new RabbitMqReceiveTransport(
            channel,
            "input",
            _ => throw new InvalidOperationException("boom"),
            hasErrorQueue: true,
            isMessageTypeRegistered: null);

        await transport.Start();

        var props = new BasicProperties();
        var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{}"));

        await consumer!.HandleBasicDeliverAsync("tag", 1, false, "ex", "rk", props, body, CancellationToken.None);

        await channel.Received()
            .BasicAckAsync(1, false, Arg.Any<CancellationToken>());
        await channel.DidNotReceive()
            .BasicNackAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
