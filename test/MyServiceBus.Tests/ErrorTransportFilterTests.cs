using System.Text;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public sealed class ErrorTransportFilterTests
{
    [Fact]
    public async Task Successful_error_copy_signals_terminal_settlement()
    {
        var errorAddress = new Uri("queue:input_error");
        var envelope = new EnvelopeMessageContext(
            Encoding.UTF8.GetBytes(
                """
                {"messageId":"00000000-0000-0000-0000-000000000001","messageType":["urn:message:MyServiceBus.Tests:TestMessage"],"message":{"value":"test"}}
                """),
            new Dictionary<string, object>());
        var receiveContext = new ReceiveContextImpl(envelope, errorAddress);
        var transportFactory = new CaptureTransportFactory();
        var context = new ConsumeContextImpl<TestMessage>(
            receiveContext,
            transportFactory,
            new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<PublishContext>()),
            new EnvelopeMessageSerializer(),
            new Uri("loopback://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());
        var configurator = new PipeConfigurator<ConsumeContext<TestMessage>>();
        configurator.UseFilter(new ErrorTransportFilter<TestMessage>());
        configurator.UseExecute(_ => throw new InvalidOperationException("boom"));
        var pipe = configurator.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipe.Send(context));

        Assert.True(ErrorTransportSettlement.WasMoved(exception));
        Assert.Equal(errorAddress, ErrorTransportSettlement.GetErrorAddress(exception));
        Assert.Equal(errorAddress, transportFactory.Address);
    }

    private sealed class TestMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class CaptureTransportFactory : ITransportFactory
    {
        public Uri? Address { get; private set; }

        public Task<ISendTransport> GetSendTransport(
            Uri address,
            CancellationToken cancellationToken = default)
        {
            Address = address;
            return Task.FromResult<ISendTransport>(new CaptureSendTransport());
        }

        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTransportTopology topology,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CaptureSendTransport : ISendTransport
    {
        public Task Send<T>(
            T message,
            SendContext context,
            CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    }
}
