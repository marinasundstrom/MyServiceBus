using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Shouldly;

namespace MyServiceBus.AmazonSqs.Tests;

public sealed class AmazonSqsTransportFactoryTests
{
    [Fact]
    public void Capabilities_describe_standard_queue_semantics()
    {
        var descriptor = TransportCapabilityDescriptors.AmazonSqs;
        descriptor.Transport.ShouldBe("amazon-sqs");
        descriptor.Get(TransportCapabilities.DirectedSend).ShouldBe(TransportCapabilitySupport.Native);
        descriptor.Get(TransportCapabilities.PublishSubscribe).ShouldBe(TransportCapabilitySupport.Native);
        descriptor.Get(TransportCapabilities.TemporaryEndpoints).ShouldBe(TransportCapabilitySupport.Emulated);
        descriptor.Get(TransportCapabilities.Ordering).ShouldBe(TransportCapabilitySupport.Unsupported);
    }

    [Fact]
    public void Configuration_validates_SQS_service_limits()
    {
        var configurator = new AmazonSqsFactoryConfigurator();
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.SetWaitTimeSeconds(21));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.SetVisibilityTimeout(43201));
        Should.Throw<ArgumentException>(() => configurator.ReceiveEndpoint("invalid.name", _ => { }));
    }

    [AmazonSqsLocalStackFact]
    public async Task Directed_send_round_trips_a_MassTransit_envelope()
    {
        var configurator = LocalStackConfigurator();
        using var sqs = CreateSqs();
        using var sns = CreateSns();
        var factory = new AmazonSqsTransportFactory(sqs, sns, configurator);
        var queue = "msb-direct-" + Guid.NewGuid().ToString("N")[..8];
        var topic = "msb-contract-" + Guid.NewGuid().ToString("N")[..8];
        var received = new TaskCompletionSource<Probe>(TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = Topology(queue, topic);
        var receiver = await factory.CreateReceiveTransport(topology, context =>
        {
            if (context.TryGetMessage<Probe>(out var message)) received.TrySetResult(message);
            return Task.CompletedTask;
        }, urn => urn == MessageUrn.For(typeof(Probe)));

        await receiver.Start();
        try
        {
            var context = SendContext(queue);
            var transport = await factory.GetSendTransport(new Uri("queue:" + queue));
            await transport.Send(new Probe("direct"), context);
            (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value.ShouldBe("direct");
        }
        finally
        {
            await receiver.Stop();
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task SNS_publication_is_delivered_to_the_subscribed_queue()
    {
        var configurator = LocalStackConfigurator();
        using var sqs = CreateSqs();
        using var sns = CreateSns();
        var factory = new AmazonSqsTransportFactory(sqs, sns, configurator);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var queue = "msb-publish-" + suffix;
        var topic = "msb-contract-" + suffix;
        var received = new TaskCompletionSource<Probe>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = await factory.CreateReceiveTransport(Topology(queue, topic), context =>
        {
            if (context.TryGetMessage<Probe>(out var message)) received.TrySetResult(message);
            return Task.CompletedTask;
        }, urn => urn == MessageUrn.For(typeof(Probe)));

        await receiver.Start();
        try
        {
            var context = SendContext(topic);
            context.DestinationAddress = factory.GetPublishAddress(topic);
            var transport = await factory.GetSendTransport(context.DestinationAddress);
            await transport.Send(new Probe("published"), context);
            (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value.ShouldBe("published");
        }
        finally
        {
            await receiver.Stop();
        }
    }

    private static AmazonSqsFactoryConfigurator LocalStackConfigurator()
    {
        var configurator = new AmazonSqsFactoryConfigurator();
        configurator.LocalstackHost();
        configurator.SetWaitTimeSeconds(1);
        return configurator;
    }

    private static AmazonSQSClient CreateSqs() => new(
        new BasicAWSCredentials("test", "test"),
        new AmazonSQSConfig
        {
            ServiceURL = "http://localhost:4566",
            AuthenticationRegion = "us-east-1"
        });

    private static AmazonSimpleNotificationServiceClient CreateSns() => new(
        new BasicAWSCredentials("test", "test"),
        new AmazonSimpleNotificationServiceConfig
        {
            ServiceURL = "http://localhost:4566",
            AuthenticationRegion = "us-east-1"
        });

    private static ReceiveEndpointTransportTopology Topology(string queue, string topic) => new(
        queue, true, false, 1,
        [new MessageBinding { MessageType = typeof(Probe), EntityName = topic }]);

    private static SendContext SendContext(string destination) => new(
        [typeof(Probe)], new EnvelopeMessageSerializer())
    {
        MessageId = Guid.NewGuid().ToString(),
        DestinationAddress = new Uri("amazonsqs://us-east-1/" + destination)
    };

    public sealed record Probe(string Value);
}
