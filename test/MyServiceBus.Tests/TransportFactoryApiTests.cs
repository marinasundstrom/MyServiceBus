using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public class TransportFactoryApiTests
{
    [Fact]
    public async Task New_transport_can_implement_only_profile_neutral_receive_topology()
    {
        ITransportFactory factory = new IntentOnlyTransportFactory();
        var topology = new ReceiveEndpointTransportTopology(
            "orders",
            durable: true,
            temporary: false,
            prefetchCount: 0,
            [new MessageBinding { MessageType = typeof(object), EntityName = "Contracts:OrderSubmitted" }]);

        var transport = await factory.CreateReceiveTransport(topology, _ => Task.CompletedTask);

        Assert.IsType<NoOpReceiveTransport>(transport);
    }

    private sealed class IntentOnlyTransportFactory : ITransportFactory
    {
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTransportTopology topology,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReceiveTransport>(new NoOpReceiveTransport());
    }

    private sealed class NoOpReceiveTransport : IReceiveTransport
    {
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
