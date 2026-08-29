using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public sealed class BusStopTimeoutTests
{
    [Fact]
    public async Task Timed_stop_reports_explicit_timeout()
    {
        var factory = new BlockingStopTransportFactory();
        var bus = new MessageBus(
            factory,
            new ServiceCollection().BuildServiceProvider(),
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("loopback://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());
        await bus.AddHandler<TestMessage>("input", "input", _ => Task.CompletedTask);
        await bus.StartAsync(CancellationToken.None);

        var timeout = TimeSpan.FromMilliseconds(50);
        var exception = await Assert.ThrowsAsync<BusStopTimeoutException>(
            () => bus.StopAsync(timeout));

        Assert.Equal(timeout, exception.Timeout);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    private sealed class TestMessage
    {
    }

    private sealed class BlockingStopTransportFactory : ITransportFactory
    {
        public Task<ISendTransport> GetSendTransport(
            Uri address,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTransportTopology topology,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReceiveTransport>(new BlockingStopTransport());
    }

    private sealed class BlockingStopTransport : IReceiveTransport
    {
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop(CancellationToken cancellationToken = default)
            => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
