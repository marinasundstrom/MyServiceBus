using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public class PublishAnonymousInterfaceTests
{
    public interface IOrder
    {
        int Id { get; }
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
        public Task<IReceiveTransport> CreateReceiveTransport(EndpointDefinition definition, Func<ReceiveContext, Task> handler, Func<string?, bool>? isMessageTypeRegistered = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    [Throws(typeof(UriFormatException))]
    public async Task Should_publish_anonymous_object_as_interface()
    {
        var factory = new StubTransportFactory();
        var sendCfg = new PipeConfigurator<SendContext>();
        var publishCfg = new PipeConfigurator<PublishContext>();
        var bus = new MessageBus(factory, new ServiceCollection().BuildServiceProvider(), new SendPipe(sendCfg.Build()), new PublishPipe(publishCfg.Build()), new EnvelopeMessageSerializer(), new Uri("rabbitmq://localhost/"), new SendContextFactory(), new PublishContextFactory());

        await bus.Publish<IOrder>(new { Id = 1 });

        var order = Assert.IsAssignableFrom<IOrder>(factory.Transport.Captured!);
        Assert.Equal(1, order.Id);
    }
}
