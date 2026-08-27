using Azure.Messaging.ServiceBus;
using Shouldly;

namespace MyServiceBus.AzureServiceBus.Tests;

public sealed class AzureServiceBusTransportFactoryTests
{
    private const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    [Fact]
    public void Profile_produces_queue_and_topic_addresses()
    {
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        configurator.SetTemporaryEndpointNameFormatter(_ => "msb-response");
        var factory = new AzureServiceBusTransportFactory(new ServiceBusClient(ConnectionString), configurator);

        factory.GetPublishAddress("orders").ShouldBe(new Uri("sb://localhost/orders?type=topic"));
        factory.GetErrorAddress("orders").ShouldBe(new Uri("sb://localhost/orders_error"));
        factory.GetFaultAddress("orders").ShouldBe(new Uri("sb://localhost/orders_fault?type=topic"));
        factory.GetTemporaryEndpointAddress("generated-response")
            .ShouldBe(new Uri("sb://localhost/msb-response?temporary=true"));
        factory.Capabilities.Transport.ShouldBe("azure-service-bus");
        factory.Capabilities.Get(TransportCapabilities.DirectedSend).ShouldBe(TransportCapabilitySupport.Native);
        factory.Capabilities.Get(TransportCapabilities.RequestResponse).ShouldBe(TransportCapabilitySupport.Emulated);
        factory.Capabilities.Get(TransportCapabilities.TemporaryEndpoints).ShouldBe(TransportCapabilitySupport.Native);
    }

    [Fact]
    public void Profile_resolves_configured_message_entity_names_for_publish()
    {
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        configurator.Message<ConfiguredMessage>(message => message.SetEntityName("configured-message"));
        var factory = new AzureServiceBusTransportFactory(new ServiceBusClient(ConnectionString), configurator);

        factory.GetPublishEntityName(typeof(ConfiguredMessage)).ShouldBe("configured-message");
        factory.GetPublishAddress(typeof(ConfiguredMessage))
            .ShouldBe(new Uri("sb://localhost/configured-message?type=topic"));
    }

    [Fact]
    public async Task Profile_rejects_unknown_absolute_entity_types()
    {
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await factory.GetSendTransport(new Uri("sb://localhost/orders?type=subscription")));
    }

    private sealed class ConfiguredMessage
    {
    }
}
