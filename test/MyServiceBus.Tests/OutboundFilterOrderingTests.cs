using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public class OutboundFilterOrderingTests
{
    sealed class DelegateFilter<TContext>(Func<TContext, IPipe<TContext>, Task> callback) : IFilter<TContext>
        where TContext : class, PipeContext
    {
        public Task Send(TContext context, IPipe<TContext> next) => callback(context, next);
    }

    sealed class RecordingTransportFactory(IList<string> calls) : ITransportFactory
    {
        public Uri GetPublishAddress(string entityName) => new($"loopback://localhost/{entityName}");

        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(new RecordingSendTransport(calls));

        public Task<IReceiveTransport> CreateReceiveTransport(
            ReceiveEndpointTransportTopology topology,
            Func<ReceiveContext, Task> handler,
            Func<string?, bool>? isMessageTypeRegistered = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    sealed class RecordingSendTransport(IList<string> calls) : ISendTransport
    {
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
        {
            calls.Add("transport");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Publish_pipeline_completes_before_send_pipeline_and_transport()
    {
        var calls = new List<string>();
        var send = new PipeConfigurator<SendContext>();
        send.UseFilter(new DelegateFilter<SendContext>(async (context, next) =>
        {
            calls.Add("send:before");
            await next.Send(context);
            calls.Add("send:after");
        }));
        var publish = new PipeConfigurator<PublishContext>();
        publish.UseFilter(new DelegateFilter<PublishContext>(async (context, next) =>
        {
            calls.Add("publish:before");
            await next.Send(context);
            calls.Add("publish:after");
        }));
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new EnvelopeMessageSerializer();
        var bus = new MessageBus(
            new RecordingTransportFactory(calls),
            services,
            new SendPipe(send.Build()),
            new PublishPipe(publish.Build()),
            serializer,
            new Uri("loopback://localhost/"),
            new SendContextFactory(),
            new PublishContextFactory());

        await bus.Publish("hello");

        Assert.Equal(
            new[] { "publish:before", "publish:after", "send:before", "send:after", "transport" },
            calls);
    }
}
