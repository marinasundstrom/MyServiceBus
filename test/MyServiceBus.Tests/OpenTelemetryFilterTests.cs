using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;
using Xunit.Sdk;

public class OpenTelemetryFilterTests
{
    class TestMessage { }

    [Fact]
    [Throws(typeof(TrueException))]
    public async Task Send_filter_adds_traceparent_header()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var cfg = new PipeConfigurator<SendContext>();
        cfg.UseFilter(new OpenTelemetrySendFilter());
        var pipe = cfg.Build();
        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(TestMessage)), new EnvelopeMessageSerializer());

        await pipe.Send(context);

        Assert.True(context.Headers.ContainsKey(MyServiceBusDiagnostics.TraceParent));
    }

    [Fact]
    [Throws(typeof(System.Text.Json.JsonException), typeof(UriFormatException), typeof(EncoderFallbackException))]
    public async Task Consume_filter_uses_parent_trace()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = MyServiceBusDiagnostics.ActivitySource.StartActivity("parent", ActivityKind.Producer);
        var headers = new Dictionary<string, object>
        {
            [MyServiceBusDiagnostics.TraceParent] = parent!.Id!,
        };
        if (!string.IsNullOrEmpty(parent.TraceStateString))
            headers[MyServiceBusDiagnostics.TraceState] = parent.TraceStateString;
        parent.Stop();

        var json = System.Text.Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var envelope = new EnvelopeMessageContext(json, headers);
        var receive = new ReceiveContextImpl(envelope, null);
        var ctx = new ConsumeContextImpl<TestMessage>(receive, new StubTransportFactory(), new SendPipe(Pipe.Empty<SendContext>()), new PublishPipe(Pipe.Empty<SendContext>()), new EnvelopeMessageSerializer(), new System.Uri("loopback://localhost/"));

        ActivityTraceId? captured = null;
        var filter = new OpenTelemetryConsumeFilter<TestMessage>();
        await filter.Send(ctx, new CapturePipe(id => { captured = id; return Task.CompletedTask; }));

        Assert.Equal(parent.TraceId, captured);
    }

    class CapturePipe : IPipe<ConsumeContext<TestMessage>>
    {
        readonly Func<ActivityTraceId?, Task> capture;
        public CapturePipe(Func<ActivityTraceId?, Task> capture) => this.capture = capture;
        public Task Send(ConsumeContext<TestMessage> context)
        {
            return capture(Activity.Current?.TraceId);
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        public Task<ISendTransport> GetSendTransport(System.Uri address, CancellationToken cancellationToken = default) => Task.FromResult<ISendTransport>(new StubSendTransport());
        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, CancellationToken cancellationToken = default) => Task.FromResult<IReceiveTransport>(new StubReceiveTransport());

        class StubSendTransport : ISendTransport
        {
            public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
        }

        class StubReceiveTransport : IReceiveTransport
        {
            public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
