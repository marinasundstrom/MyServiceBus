using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace MyServiceBus.AzureServiceBus.Tests;

public class PublishTopologyTests
{
    [Fact]
    public async Task Creates_shared_direct_forwarding_bindings_and_retries_failed_provisioning()
    {
        const string connection = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;";
        var config = new AzureServiceBusFactoryConfigurator();
        config.Host(connection);
        var admin = new TestAdministration();
        await using var client = new ServiceBusClient(connection);
        var factory = new AzureServiceBusTransportFactory(client, config, administrationClient: admin);
        string[] entities = ["concrete", "base", "interface", "interface"];
        admin.FailNext = true;
        await Assert.ThrowsAsync<AzureServiceBusTransportException>(() => factory.PreparePublishTopology(entities));
        await factory.PreparePublishTopology(entities);
        await factory.PreparePublishTopology(entities);
        Assert.Equal(["base", "concrete", "interface"], admin.Topics.Order().ToArray());
        Assert.Equal(2, admin.Subscriptions.Count);
        Assert.All(admin.Subscriptions, subscription => Assert.Equal("concrete", subscription.TopicName));
        Assert.Equal(["base", "interface"], admin.Subscriptions.Select(s => s.ForwardTo).Order().ToArray());
        Assert.All(admin.Subscriptions, subscription => Assert.Matches("^msb-[0-9a-f]{32}$", subscription.SubscriptionName));
    }

    private sealed class TestAdministration : ServiceBusAdministrationClient
    {
        public bool FailNext;
        public HashSet<string> Topics { get; } = [];
        public List<CreateSubscriptionOptions> Subscriptions { get; } = [];
        public override Task<Azure.Response<bool>> TopicExistsAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Azure.Response.FromValue(Topics.Contains(name), null!));
        public override Task<Azure.Response<TopicProperties>> CreateTopicAsync(string name, CancellationToken cancellationToken = default)
        {
            if (FailNext) { FailNext = false; throw new InvalidOperationException("temporary provisioning failure"); }
            Topics.Add(name);
            return Task.FromResult<Azure.Response<TopicProperties>>(null!);
        }
        public override Task<Azure.Response<bool>> SubscriptionExistsAsync(string topic, string subscription, CancellationToken cancellationToken = default)
            => Task.FromResult(Azure.Response.FromValue(false, null!));
        public override Task<Azure.Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CancellationToken cancellationToken = default)
        {
            Subscriptions.Add(options);
            return Task.FromResult<Azure.Response<SubscriptionProperties>>(null!);
        }
    }
}
