using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public class SendRespondAnonymousInterfaceTests
{
    public interface ICommand
    {
        int Id { get; }
    }

    public interface IReply
    {
        string Value { get; }
    }

    class CaptureSendTransport : ISendTransport
    {
        public object? Captured;

        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
        {
            Captured = message;
            return Task.CompletedTask;
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public readonly CaptureSendTransport Transport = new();

        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default) => Task.FromResult<ISendTransport>(Transport);

        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    class TestMessageContext : IMessageContext
    {
        public Guid MessageId { get; } = Guid.NewGuid();
        public Guid? CorrelationId => null;
        public IList<string> MessageType { get; } = new List<string>();
        public Uri? ResponseAddress { get; set; }
        public Uri? FaultAddress => null;
        public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
        public DateTimeOffset SentTime { get; } = DateTimeOffset.UtcNow;
        [Throws(typeof(ObjectDisposedException))]
        public bool TryGetMessage<T>(out T? message) where T : class { message = null; return false; }
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(IsAssignableFromException))]
    public async Task Should_send_anonymous_object_as_interface()
    {
        var factory = new StubTransportFactory();
        var sendCfg = new PipeConfigurator<SendContext>();
        var endpoint = new TransportSendEndpoint(factory, new SendPipe(sendCfg.Build()), new EnvelopeMessageSerializer(), new Uri("loopback://localhost/queue"), new Uri("loopback://localhost/"), new SendContextFactory(), null);

        await endpoint.Send<ICommand>(new { Id = 42 });

        var cmd = Assert.IsAssignableFrom<ICommand>(factory.Transport.Captured!);
        Assert.Equal(42, cmd.Id);
    }

    [Fact]
    [Throws(typeof(InvalidOperationException), typeof(UriFormatException))]
    public async Task Should_respond_anonymous_object_as_interface()
    {
        var factory = new StubTransportFactory();
        var sendCfg = new PipeConfigurator<SendContext>();
        var publishCfg = new PipeConfigurator<PublishContext>();
        var msgCtx = new TestMessageContext { ResponseAddress = new Uri("loopback://localhost/reply") };
        var receiveContext = new ReceiveContextImpl(msgCtx);
        var context = new ConsumeContextImpl<object>(receiveContext, factory, new SendPipe(sendCfg.Build()), new PublishPipe(publishCfg.Build()), new EnvelopeMessageSerializer(), new Uri("loopback://localhost/"), new SendContextFactory(), new PublishContextFactory());

        await context.RespondAsync<IReply>(new { Value = "hi" });

        var reply = Assert.IsAssignableFrom<IReply>(factory.Transport.Captured!);
        Assert.Equal("hi", reply.Value);
    }
}
