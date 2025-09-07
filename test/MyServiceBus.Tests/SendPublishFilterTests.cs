using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;
using Xunit.Sdk;

public class SendPublishFilterTests
{
    class TestMessage { }

    class CaptureSendTransport : ISendTransport
    {
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
            => Task.CompletedTask;
    }

    class StubTransportFactory : ITransportFactory
    {
        public readonly CaptureSendTransport Transport = new();

        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(Transport);
        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(TrueException))]
    public async Task Executes_send_and_publish_filters()
    {
        var sendExecuted = false;
        var publishExecuted = false;
        var sendCfg = new PipeConfigurator<SendContext>();
        sendCfg.UseExecute(ctx => { sendExecuted = true; return Task.CompletedTask; });
        var publishCfg = new PipeConfigurator<PublishContext>();
        publishCfg.UseExecute(ctx => { publishExecuted = true; return Task.CompletedTask; });

        var bus = new MyServiceBus.MessageBus(new StubTransportFactory(), new ServiceCollection().BuildServiceProvider(),
            new SendPipe(sendCfg.Build()), new PublishPipe(publishCfg.Build()), new EnvelopeMessageSerializer(),
            new Uri("loopback://localhost/"), new SendContextFactory(), new PublishContextFactory());

        await bus.PublishAsync(new TestMessage());

        Assert.True(sendExecuted);
        Assert.True(publishExecuted);
    }
}
