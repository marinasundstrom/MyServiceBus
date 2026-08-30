using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using MassTransit;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using TestApp;

namespace MyServiceBus.AmazonSqs.Tests;

public sealed class MassTransitAmazonSqsInteropTests
{
    private const string ServiceUrl = "http://localhost:4566";

    [Fact]
    public void Default_message_entity_name_matches_MassTransit()
    {
        var configurator = new AmazonSqsFactoryConfigurator();
        var formatter = new MassTransit.AmazonSqsTransport.AmazonSqsMessageNameFormatter();

        Assert.Equal(formatter.GetMessageName(typeof(InteropMessage)),
            configurator.GetEntityName(typeof(InteropMessage)));
        Assert.Equal(formatter.GetMessageName(typeof(FormatterProbe<InteropMessage>)),
            configurator.GetEntityName(typeof(FormatterProbe<InteropMessage>)));
    }

    [AmazonSqsLocalStackFact]
    public async Task MyServiceBus_direct_send_delivers_to_MassTransit_consumer()
    {
        var names = Names.Create("msb-send-mt");
        var received = Completion();
        var bus = CreateMassTransitBus(names.Topic, cfg =>
            cfg.ReceiveEndpoint(names.Queue, endpoint => endpoint.Handler<InteropMessage>(context =>
            {
                received.TrySetResult(context.Message);
                return Task.CompletedTask;
            })));

        await bus.StartAsync();
        try
        {
            var factory = CreateMyServiceBusFactory(names.Topic);
            await Send(factory, new Uri("queue:" + names.Queue), "from-myservicebus");

            Assert.Equal("from-myservicebus",
                (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value);
        }
        finally
        {
            await bus.StopAsync();
            await DeleteTopology(names);
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task MassTransit_direct_send_delivers_to_MyServiceBus_consumer()
    {
        var names = Names.Create("mt-send-msb");
        var received = Completion();
        var factory = CreateMyServiceBusFactory(names.Topic);
        var receiver = await CreateReceiver(factory, names, received);
        var bus = CreateMassTransitBus(names.Topic);
        var receiverStarted = false;

        await bus.StartAsync();
        try
        {
            var endpoint = await bus.GetSendEndpoint(new Uri("queue:" + names.Queue));
            await endpoint.Send(new InteropMessage { Value = "from-masstransit" });

            using (var sqs = CreateSqs())
            {
                var queueUrl = (await sqs.GetQueueUrlAsync(names.Queue)).QueueUrl;
                var native = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = 2,
                    MessageAttributeNames = ["All"]
                });
                var sent = Assert.Single(native.Messages ?? []);
                await sqs.ChangeMessageVisibilityAsync(queueUrl, sent.ReceiptHandle, 0);
            }

            await receiver.Start();
            receiverStarted = true;

            Assert.Equal("from-masstransit",
                (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value);
        }
        finally
        {
            await bus.StopAsync();
            if (receiverStarted)
                await receiver.Stop();
            await DeleteTopology(names);
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task MyServiceBus_publish_delivers_to_MassTransit_consumer()
    {
        var names = Names.Create("msb-publish-mt");
        var received = Completion();
        var bus = CreateMassTransitBus(names.Topic, cfg =>
            cfg.ReceiveEndpoint(names.Queue, endpoint => endpoint.Handler<InteropMessage>(context =>
            {
                received.TrySetResult(context.Message);
                return Task.CompletedTask;
            })));

        await bus.StartAsync();
        try
        {
            var factory = CreateMyServiceBusFactory(names.Topic);
            await Send(factory, factory.GetPublishAddress(names.Topic), "published-by-myservicebus");

            Assert.Equal("published-by-myservicebus",
                (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value);
        }
        finally
        {
            await bus.StopAsync();
            await DeleteTopology(names);
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task MassTransit_publish_delivers_to_MyServiceBus_consumer()
    {
        var names = Names.Create("mt-publish-msb");
        var received = Completion();
        var factory = CreateMyServiceBusFactory(names.Topic);
        var receiver = await CreateReceiver(factory, names, received);
        var bus = CreateMassTransitBus(names.Topic);

        await receiver.Start();
        await bus.StartAsync();
        try
        {
            await bus.Publish(new InteropMessage { Value = "published-by-masstransit" });

            Assert.Equal("published-by-masstransit",
                (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value);
        }
        finally
        {
            await bus.StopAsync();
            await receiver.Stop();
            await DeleteTopology(names);
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task Java_MyServiceBus_direct_send_delivers_to_MassTransit_consumer()
    {
        var names = Names.Create("java-send-mt");
        var received = Completion();
        var bus = CreateMassTransitBus(names.Topic, cfg =>
            cfg.ReceiveEndpoint(names.Queue, endpoint => endpoint.Handler<InteropMessage>(context =>
            {
                received.TrySetResult(context.Message);
                return Task.CompletedTask;
            })));
        await bus.StartAsync();
        using var java = AmazonSqsJavaInteropPeer.Start(
            "amazon-send", names.Queue, names.Topic, "from-java");
        try
        {
            await AmazonSqsJavaInteropPeer.WaitForOutput(java, "SENT", TimeSpan.FromMinutes(2));
            Assert.Equal("from-java", (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value);
            await AssertJavaExit(java);
        }
        finally
        {
            await bus.StopAsync();
            StopJava(java);
            await DeleteTopology(names);
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task MassTransit_direct_send_delivers_to_Java_MyServiceBus_consumer()
    {
        var names = Names.Create("mt-send-java");
        using var java = AmazonSqsJavaInteropPeer.Start(
            "amazon-consume", names.Queue, names.Topic, "from-masstransit");
        var bus = CreateMassTransitBus(names.Topic);
        try
        {
            await AmazonSqsJavaInteropPeer.WaitForOutput(java, "READY", TimeSpan.FromMinutes(2));
            await bus.StartAsync();
            var endpoint = await bus.GetSendEndpoint(new Uri("queue:" + names.Queue));
            await endpoint.Send(new InteropMessage { Value = "from-masstransit" });
            await AmazonSqsJavaInteropPeer.WaitForOutput(java, "RECEIVED", TimeSpan.FromSeconds(20));
            await AssertJavaExit(java);
        }
        finally
        {
            await bus.StopAsync();
            StopJava(java);
            await DeleteTopology(names);
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task Java_MyServiceBus_publish_delivers_to_MassTransit_consumer()
    {
        var names = Names.Create("java-publish-mt");
        var received = Completion();
        var bus = CreateMassTransitBus(names.Topic, cfg =>
            cfg.ReceiveEndpoint(names.Queue, endpoint => endpoint.Handler<InteropMessage>(context =>
            {
                received.TrySetResult(context.Message);
                return Task.CompletedTask;
            })));
        await bus.StartAsync();
        using var java = AmazonSqsJavaInteropPeer.Start(
            "amazon-publish", names.Queue, names.Topic, "published-by-java");
        try
        {
            await AmazonSqsJavaInteropPeer.WaitForOutput(java, "SENT", TimeSpan.FromMinutes(2));
            Assert.Equal("published-by-java",
                (await received.Task.WaitAsync(TimeSpan.FromSeconds(20))).Value);
            await AssertJavaExit(java);
        }
        finally
        {
            await bus.StopAsync();
            StopJava(java);
            await DeleteTopology(names);
        }
    }

    [AmazonSqsLocalStackFact]
    public async Task MassTransit_publish_delivers_to_Java_MyServiceBus_consumer()
    {
        var names = Names.Create("mt-publish-java");
        using var java = AmazonSqsJavaInteropPeer.Start(
            "amazon-consume", names.Queue, names.Topic, "published-by-masstransit");
        var bus = CreateMassTransitBus(names.Topic);
        try
        {
            await AmazonSqsJavaInteropPeer.WaitForOutput(java, "READY", TimeSpan.FromMinutes(2));
            await bus.StartAsync();
            await bus.Publish(new InteropMessage { Value = "published-by-masstransit" });
            await AmazonSqsJavaInteropPeer.WaitForOutput(java, "RECEIVED", TimeSpan.FromSeconds(20));
            await AssertJavaExit(java);
        }
        finally
        {
            await bus.StopAsync();
            StopJava(java);
            await DeleteTopology(names);
        }
    }

    private static TaskCompletionSource<InteropMessage> Completion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task AssertJavaExit(System.Diagnostics.Process process)
    {
        await AmazonSqsJavaInteropPeer.WaitForExit(process, TimeSpan.FromSeconds(10));
        Assert.Equal(0, process.ExitCode);
    }

    private static void StopJava(System.Diagnostics.Process process)
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
    }

    private static IBusControl CreateMassTransitBus(
        string topicName,
        Action<IAmazonSqsBusFactoryConfigurator>? configure = null) =>
        MassTransit.Bus.Factory.CreateUsingAmazonSqs(cfg =>
        {
            cfg.WaitTimeSeconds = 1;
            cfg.Host(new Uri("amazonsqs://localhost:4566"), host =>
            {
                host.AccessKey("test");
                host.SecretKey("test");
                host.Config(new AmazonSQSConfig
                {
                    ServiceURL = ServiceUrl,
                    AuthenticationRegion = "us-east-1"
                });
                host.Config(new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = ServiceUrl,
                    AuthenticationRegion = "us-east-1"
                });
            });
            cfg.Message<InteropMessage>(message => message.SetEntityName(topicName));
            configure?.Invoke(cfg);
        });

    private static AmazonSqsTransportFactory CreateMyServiceBusFactory(string topicName)
    {
        var configurator = new AmazonSqsFactoryConfigurator();
        configurator.LocalstackHost(ServiceUrl);
        configurator.SetWaitTimeSeconds(1);
        configurator.Message<InteropMessage>(message => message.SetEntityName(topicName));
        return new AmazonSqsTransportFactory(CreateSqs(), CreateSns(), configurator);
    }

    private static Task<IReceiveTransport> CreateReceiver(
        AmazonSqsTransportFactory factory,
        Names names,
        TaskCompletionSource<InteropMessage> received) =>
        factory.CreateReceiveTransport(
            new ReceiveEndpointTransportTopology(
                names.Queue,
                true,
                false,
                1,
                [new MessageBinding { MessageType = typeof(InteropMessage), EntityName = names.Topic }]),
            context =>
            {
                if (context.TryGetMessage<InteropMessage>(out var message))
                    received.TrySetResult(message);
                else
                    received.TrySetException(new InvalidOperationException(
                        $"Could not deserialize MassTransit envelope types: {string.Join(", ", context.MessageType)}"));
                return Task.CompletedTask;
            },
            messageType => messageType == MessageUrn.For(typeof(InteropMessage)));

    private static async Task Send(AmazonSqsTransportFactory factory, Uri address, string value)
    {
        var context = new SendContext([typeof(InteropMessage)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString(),
            DestinationAddress = address
        };
        await (await factory.GetSendTransport(address)).Send(new InteropMessage { Value = value }, context);
    }

    private static AmazonSQSClient CreateSqs() => new(
        new BasicAWSCredentials("test", "test"),
        new AmazonSQSConfig { ServiceURL = ServiceUrl, AuthenticationRegion = "us-east-1" });

    private static AmazonSimpleNotificationServiceClient CreateSns() => new(
        new BasicAWSCredentials("test", "test"),
        new AmazonSimpleNotificationServiceConfig
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = "us-east-1"
        });

    private static async Task DeleteTopology(Names names)
    {
        using var sqs = CreateSqs();
        using var sns = CreateSns();
        foreach (var queueName in new[] { names.Queue, names.Queue + "_error", names.Queue + "_skipped" })
        {
            try
            {
                var queueUrl = (await sqs.GetQueueUrlAsync(queueName)).QueueUrl;
                await sqs.DeleteQueueAsync(queueUrl);
            }
            catch (QueueDoesNotExistException)
            {
            }
        }

        string? nextToken = null;
        do
        {
            var topics = await sns.ListTopicsAsync(new ListTopicsRequest { NextToken = nextToken });
            foreach (var topic in topics.Topics ?? [])
            {
                if (topic.TopicArn.EndsWith(':' + names.Topic, StringComparison.Ordinal) ||
                    topic.TopicArn.EndsWith(':' + names.Queue + "_fault", StringComparison.Ordinal))
                    await sns.DeleteTopicAsync(topic.TopicArn);
            }
            nextToken = topics.NextToken;
        } while (!string.IsNullOrWhiteSpace(nextToken));
    }

    private sealed record Names(string Queue, string Topic)
    {
        public static Names Create(string prefix)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            return new Names($"{prefix}-{suffix}", $"{prefix}-contract-{suffix}");
        }
    }

    private sealed class FormatterProbe<T>;

}
