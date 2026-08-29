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
    public async Task Nacks_message_for_redelivery_when_handler_fails_before_error_move()
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
            errorAddress: new Uri("rabbitmq://broker/exchange/input_error"),
            faultAddress: new Uri("rabbitmq://broker/exchange/input_fault"),
            isMessageTypeRegistered: null);

        await transport.Start();

        var props = new BasicProperties();
        var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{}"));

        await consumer!.HandleBasicDeliverAsync("tag", 1, false, "ex", "rk", props, body, CancellationToken.None);

        await channel.DidNotReceive()
            .BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await channel.Received()
            .BasicNackAsync(1, false, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Acks_message_after_confirmed_error_move()
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

        var failure = new InvalidOperationException("boom");
        ErrorTransportSettlement.MarkMoved(failure, new Uri("rabbitmq://broker/exchange/input_error"));
        var transport = new RabbitMqReceiveTransport(
            channel,
            "input",
            _ => throw failure,
            errorAddress: new Uri("rabbitmq://broker/exchange/input_error"),
            faultAddress: new Uri("rabbitmq://broker/exchange/input_fault"),
            isMessageTypeRegistered: null);

        await transport.Start();
        await consumer!.HandleBasicDeliverAsync(
            "tag",
            1,
            false,
            "ex",
            "rk",
            new BasicProperties(),
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{}")),
            CancellationToken.None);

        await channel.Received()
            .BasicAckAsync(1, false, Arg.Any<CancellationToken>());
        await channel.DidNotReceive()
            .BasicNackAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nacks_message_when_skipped_move_is_not_confirmed()
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
        channel.BasicPublishAsync(
                "input_skipped",
                string.Empty,
                true,
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new InvalidOperationException("confirm failed")));

        var transport = new RabbitMqReceiveTransport(
            channel,
            "input",
            _ => Task.CompletedTask,
            errorAddress: new Uri("rabbitmq://broker/exchange/input_error"),
            faultAddress: new Uri("rabbitmq://broker/exchange/input_fault"),
            isMessageTypeRegistered: _ => false);

        await transport.Start();
        var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(
            "{\"messageType\":[\"urn:message:test\"],\"message\":{}}"));
        await consumer!.HandleBasicDeliverAsync(
            "tag",
            1,
            false,
            "ex",
            "rk",
            new BasicProperties(),
            body,
            CancellationToken.None);

        await channel.DidNotReceive()
            .BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await channel.Received()
            .BasicNackAsync(1, false, true, Arg.Any<CancellationToken>());
    }
}
