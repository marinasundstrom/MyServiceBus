using System.Collections.Concurrent;
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Shouldly;

namespace MyServiceBus.AmazonSqs.Tests;

public sealed class AmazonSqsCloudTests
{
    [AmazonSqsCloudFact]
    public async Task Standard_queue_send_and_SNS_publish_round_trip_with_cleanup()
    {
        var regionName = Environment.GetEnvironmentVariable("AWS_REGION")!;
        var region = RegionEndpoint.GetBySystemName(regionName);
        using var sqs = new AmazonSQSClient(region);
        using var sns = new AmazonSimpleNotificationServiceClient(region);
        var configurator = new AmazonSqsFactoryConfigurator();
        configurator.Host(regionName);
        configurator.SetWaitTimeSeconds(2);
        var factory = new AmazonSqsTransportFactory(sqs, sns, configurator);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = "msb-cloud-" + suffix;
        var entityName = "msb-cloud-contract-" + suffix;
        var received = new ConcurrentDictionary<string, TaskCompletionSource<Probe>>(StringComparer.Ordinal)
        {
            ["direct"] = Completion(),
            ["published"] = Completion()
        };
        IReceiveTransport? receiver = null;

        try
        {
            receiver = await factory.CreateReceiveTransport(Topology(queueName, entityName), context =>
            {
                if (context.TryGetMessage<Probe>(out var message) &&
                    received.TryGetValue(message.Value, out var completion))
                    completion.TrySetResult(message);
                return Task.CompletedTask;
            }, urn => urn == MessageUrn.For(typeof(Probe)));
            await receiver.Start();

            await Send(factory, factory.GetSendTransport(new Uri("queue:" + queueName)),
                queueName, new Probe("direct"));
            await Send(factory, factory.GetSendTransport(factory.GetPublishAddress(entityName)),
                entityName, new Probe("published"), publish: true);

            (await received["direct"].Task.WaitAsync(TimeSpan.FromSeconds(30))).Value.ShouldBe("direct");
            (await received["published"].Task.WaitAsync(TimeSpan.FromSeconds(30))).Value.ShouldBe("published");
        }
        finally
        {
            if (receiver is not null)
                await receiver.Stop();
            await DeleteTopic(sns, entityName);
            await DeleteTopic(sns, AmazonSqsEntityNameForTest.Companion(queueName, "_fault"));
            await DeleteQueue(sqs, queueName);
            await DeleteQueue(sqs, AmazonSqsEntityNameForTest.Companion(queueName, "_error"));
            await DeleteQueue(sqs, AmazonSqsEntityNameForTest.Companion(queueName, "_skipped"));
        }
    }

    private static TaskCompletionSource<Probe> Completion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ReceiveEndpointTransportTopology Topology(string queue, string topic) => new(
        queue, true, false, 2,
        [new MessageBinding { MessageType = typeof(Probe), EntityName = topic }]);

    private static async Task Send(
        AmazonSqsTransportFactory factory,
        Task<ISendTransport> transportTask,
        string destination,
        Probe message,
        bool publish = false)
    {
        var context = new SendContext([typeof(Probe)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString(),
            DestinationAddress = publish
                ? factory.GetPublishAddress(destination)
                : new Uri($"amazonsqs://{Environment.GetEnvironmentVariable("AWS_REGION")}/{destination}")
        };
        await (await transportTask).Send(message, context);
    }

    private static async Task DeleteQueue(IAmazonSQS sqs, string queueName)
    {
        try
        {
            var queueUrl = (await sqs.GetQueueUrlAsync(queueName)).QueueUrl;
            await sqs.DeleteQueueAsync(queueUrl);
        }
        catch (Amazon.SQS.Model.QueueDoesNotExistException)
        {
        }
    }

    private static async Task DeleteTopic(IAmazonSimpleNotificationService sns, string topicName)
    {
        string? nextToken = null;
        do
        {
            var topics = await sns.ListTopicsAsync(new ListTopicsRequest { NextToken = nextToken });
            var topic = topics.Topics.FirstOrDefault(x => x.TopicArn.EndsWith(':' + topicName, StringComparison.Ordinal));
            if (topic is not null)
            {
                var subscriptions = await sns.ListSubscriptionsByTopicAsync(topic.TopicArn);
                foreach (var subscription in subscriptions.Subscriptions ?? [])
                    if (!string.IsNullOrWhiteSpace(subscription.SubscriptionArn) &&
                        subscription.SubscriptionArn != "PendingConfirmation")
                        await sns.UnsubscribeAsync(subscription.SubscriptionArn);
                await sns.DeleteTopicAsync(topic.TopicArn);
                return;
            }
            nextToken = topics.NextToken;
        } while (!string.IsNullOrWhiteSpace(nextToken));
    }

    public sealed record Probe(string Value);

    private static class AmazonSqsEntityNameForTest
    {
        public static string Companion(string value, string suffix) =>
            (value.Length > 80 - suffix.Length ? value[..(80 - suffix.Length)] : value) + suffix;
    }
}
