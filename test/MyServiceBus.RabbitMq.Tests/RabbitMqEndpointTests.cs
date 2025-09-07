using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqEndpointTests
{
    class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    [Fact]
    [Throws(typeof(JsonException), typeof(OperationCanceledException), typeof(KeyNotFoundException), typeof(AggregateException))]
    public async Task ReadAsync_Receives_Envelope()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();
        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>()).Returns(Task.FromResult(channel));

        var factory = Substitute.For<IConnectionFactory>();
        factory.CreateConnectionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(connection));
        var provider = new ConnectionProvider(factory);

        var expected = new Envelope<TestMessage>
        {
            MessageId = Guid.Empty,
            MessageType = new(),
            Message = new TestMessage { Text = "hi" }
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(expected, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var props = new BasicProperties { ContentType = "application/vnd.masstransit+json" };
        var result = new BasicGetResult(1, false, "", "", 0, props, bytes);

        channel.BasicGetAsync("test-queue", false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BasicGetResult?>(result),
                Task.FromResult<BasicGetResult?>(null));

        var endpoint = new RabbitMqEndpoint(sendEndpoint, provider, "test-queue");
        using var cts = new CancellationTokenSource();

        var enumerator = endpoint.ReadAsync(cts.Token).GetAsyncEnumerator();
        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        var received = enumerator.Current;
        received.TryGetMessage<TestMessage>(out var got).ShouldBeTrue();
        got!.Text.ShouldBe("hi");

        cts.Cancel();
        (await enumerator.MoveNextAsync()).ShouldBeFalse();
        await enumerator.DisposeAsync();
    }
}
