using NSubstitute;
using RabbitMQ.Client;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class SkippedQueueTests
{
    [Fact]
    [Throws(typeof(JsonException), typeof(UriFormatException), typeof(OperationCanceledException), typeof(System.IO.IOException))]
    public async Task Moves_unknown_messages_to_skipped_queue()
    {
        var channel = Substitute.For<IChannel>();
        AsyncEventingBasicConsumer? consumer = null;
        channel.BasicConsumeAsync(
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Do<IBasicConsumer>(c => consumer = (AsyncEventingBasicConsumer)c),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("tag"));

        var transport = new RabbitMqReceiveTransport(channel, "input", ctx => throw new UnknownMessageTypeException(ctx.MessageType.FirstOrDefault()), true);
        await transport.Start();

        var props = new BasicProperties();
        var body = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        await consumer!.HandleBasicDeliver("tag", 1, false, "ex", "rk", props, body);

        await channel.Received().BasicPublishAsync("input_skipped", string.Empty, false, props, body, Arg.Any<CancellationToken>());
        await channel.Received().BasicAckAsync(1, false, Arg.Any<CancellationToken>());
    }
}
