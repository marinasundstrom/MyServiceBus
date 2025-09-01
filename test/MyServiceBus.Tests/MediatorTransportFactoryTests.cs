using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class MediatorTransportFactoryTests
{
    class SampleMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    class ConsumerMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    class SampleConsumer : IConsumer<ConsumerMessage>
    {
        public static TaskCompletionSource<ConsumerMessage> Received = new();

        [Throws(typeof(ObjectDisposedException))]
        public Task Consume(ConsumeContext<ConsumerMessage> context)
        {
            Received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public async Task Send_Invokes_RegisteredHandler()
    {
        var factory = new MediatorTransportFactory();
        var tcs = new TaskCompletionSource<SampleMessage>();
        var topology = new ReceiveEndpointTopology
        {
            ExchangeName = "test",
            QueueName = "queue",
            RoutingKey = ""
        };

        var receive = await factory.CreateReceiveTransport(topology, [Throws(typeof(ObjectDisposedException))] (ctx) =>
        {
            ctx.TryGetMessage<SampleMessage>(out var msg);
            tcs.SetResult(msg!);
            return Task.CompletedTask;
        });

        await receive.Start();

        // Use a URI with a path segment so the exchange can be extracted
        var send = await factory.GetSendTransport(new Uri("loopback://localhost/test"));

        var serializer = new EnvelopeMessageSerializer();
        var sendContext = new SendContext(new[] { typeof(SampleMessage) }, serializer)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await send.Send(new SampleMessage { Value = "hi" }, sendContext);

        var message = await tcs.Task;
        Assert.Equal("hi", message.Value);

        await receive.Stop();
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(Exception))]
    public async Task Publish_delivers_message_to_registered_consumer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<SampleConsumer>();
        });

        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        SampleConsumer.Received = new TaskCompletionSource<ConsumerMessage>();

        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.Publish(new ConsumerMessage { Value = "hello" });

        var message = await SampleConsumer.Received.Task;
        Assert.Equal("hello", message.Value);

        await hosted.StopAsync(CancellationToken.None);
    }
}
