using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

public class PublishHeaderTests
{
    class TestMessage { }

    class CaptureSendTransport : ISendTransport
    {
        public SendContext? Captured;
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
        {
            Captured = context;
            return Task.CompletedTask;
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public readonly CaptureSendTransport Transport = new();
        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(Transport);
        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(ArgumentNullException))]
    public async Task Applies_headers_to_context()
    {
        var factory = new StubTransportFactory();
        var bus = new MessageBus(factory, new ServiceCollection().BuildServiceProvider(),
            new SendPipe(Pipe.Empty<SendContext>()), new PublishPipe(Pipe.Empty<SendContext>()), new EnvelopeMessageSerializer(),
            new Uri("loopback://localhost/"));

        await bus.PublishAsync(new TestMessage(), [Throws(typeof(NotSupportedException))] (ctx) => ctx.Headers["foo"] = "bar");

        Assert.NotNull(factory.Transport.Captured);
        Assert.True(factory.Transport.Captured!.Headers.ContainsKey("foo"));
        Assert.Equal("bar", factory.Transport.Captured!.Headers["foo"]);
    }
}
