using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

namespace MyServiceBus.Tests;

public class SendEndpointAddressTests
{
    class TestMessage { }

    class CaptureSendTransport : ISendTransport
    {
        public SendContext? Context;
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
        {
            Context = context;
            return Task.CompletedTask;
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public readonly CaptureSendTransport Transport = new();

        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ISendTransport>(Transport);
        }

        public Task<IReceiveTransport> CreateReceiveTransport(EndpointDefinition definition, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    [Throws(typeof(UriFormatException))]
    public async Task SendEndpoint_sets_source_and_destination_addresses()
    {
        var factory = new StubTransportFactory();
        var bus = new MessageBus(factory, new ServiceCollection().BuildServiceProvider(), new SendPipe(Pipe.Empty<SendContext>()), new PublishPipe(Pipe.Empty<PublishContext>()), new EnvelopeMessageSerializer(), new Uri("rabbitmq://localhost/"), new SendContextFactory(), new PublishContextFactory());

        var endpoint = await bus.GetSendEndpoint(new Uri(bus.Address, "queue/test"));
        await endpoint.Send(new TestMessage());

        Assert.Equal(bus.Address, factory.Transport.Context!.SourceAddress);
        Assert.Equal(new Uri(bus.Address, "queue/test"), factory.Transport.Context.DestinationAddress);
    }
}
