using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

namespace MyServiceBus.Tests;

public class PublishContextAddressTests
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

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default) => Task.FromResult<ISendTransport>(Transport);
        [Throws(typeof(NotImplementedException))]
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    [Throws(typeof(UriFormatException))]
    public async Task Sets_source_and_destination_addresses()
    {
        var factory = new StubTransportFactory();
        var sendCfg = new PipeConfigurator<SendContext>();
        var publishCfg = new PipeConfigurator<PublishContext>();
        var bus = new MessageBus(factory, new ServiceCollection().BuildServiceProvider(), new SendPipe(sendCfg.Build()), new PublishPipe(publishCfg.Build()), new EnvelopeMessageSerializer(), new Uri("rabbitmq://localhost/"), new SendContextFactory(), new PublishContextFactory());

        await bus.Publish(new TestMessage());

        Assert.Equal(bus.Address, factory.Transport.Captured!.SourceAddress);
        Assert.Equal(new Uri(bus.Address, $"exchange/{EntityNameFormatter.Format(typeof(TestMessage))}"), factory.Transport.Captured.DestinationAddress);
    }
}
