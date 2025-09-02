using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

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
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(Transport);
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task Executes_send_and_publish_filters()
    {
        var sendExecuted = false;
        var publishExecuted = false;
        var sendCfg = new PipeConfigurator<SendContext>();
        sendCfg.UseExecute(ctx => { sendExecuted = true; return Task.CompletedTask; });
        var publishCfg = new PipeConfigurator<SendContext>();
        publishCfg.UseExecute(ctx => { publishExecuted = true; return Task.CompletedTask; });

        var bus = new MyMessageBus(new StubTransportFactory(), new ServiceCollection().BuildServiceProvider(),
            new SendPipe(sendCfg.Build()), new PublishPipe(publishCfg.Build()), new EnvelopeMessageSerializer());

        await bus.Publish(new TestMessage());

        Assert.True(sendExecuted);
        Assert.True(publishExecuted);
    }
}
