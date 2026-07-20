using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;

public class TransportCapabilityTests
{
    [Fact]
    public void RabbitMq_descriptor_declares_portable_capabilities()
    {
        var descriptor = TransportCapabilityDescriptors.RabbitMq;

        Assert.Equal(1, descriptor.Version);
        Assert.Equal("rabbitmq", descriptor.Transport);
        Assert.Equal(TransportCapabilitySupport.Native, descriptor.Get(TransportCapabilities.DirectedSend));
        Assert.Equal(TransportCapabilitySupport.Emulated, descriptor.Get(TransportCapabilities.Retry));
        Assert.Equal(TransportCapabilitySupport.Unsupported, descriptor.Get(TransportCapabilities.Redelivery));
        Assert.Equal(TransportCapabilitySupport.Unsupported, descriptor.Get(TransportCapabilities.Replay));
        Assert.Equal(TransportCapabilitySupport.Unsupported, descriptor.Get("unknownCapability"));
    }

    [Fact]
    public void InMemory_factory_exposes_its_descriptor()
    {
        ITransportFactory factory = new MediatorTransportFactory();

        Assert.Equal("in-memory", factory.Capabilities.Transport);
        Assert.Equal(
            TransportCapabilitySupport.Unsupported,
            factory.Capabilities.Get(TransportCapabilities.Durability));
    }

    [Fact]
    public void Support_values_have_stable_protocol_names()
    {
        Assert.Equal("\"native\"", JsonSerializer.Serialize(TransportCapabilitySupport.Native));
        Assert.Equal("\"emulated\"", JsonSerializer.Serialize(TransportCapabilitySupport.Emulated));
        Assert.Equal("\"unsupported\"", JsonSerializer.Serialize(TransportCapabilitySupport.Unsupported));
        Assert.Contains("\"version\":1", JsonSerializer.Serialize(TransportCapabilityDescriptors.RabbitMq));
    }

    [Fact]
    public async Task Startup_rejects_an_unsupported_required_capability()
    {
        var services = new ServiceCollection();
        services.AddServiceBus(configurator =>
        {
            configurator.RequireTransportCapability(TransportCapabilities.Durability);
            configurator.UsingMediator();
        });
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();

        var exception = await Assert.ThrowsAsync<UnsupportedTransportCapabilityException>(
            () => bus.StartAsync(CancellationToken.None));

        Assert.Equal("in-memory", exception.Transport);
        Assert.Equal(TransportCapabilities.Durability, exception.Capability);
    }

    [Fact]
    public async Task Startup_accepts_emulation_unless_native_support_is_required()
    {
        var availableServices = new ServiceCollection();
        availableServices.AddServiceBus(configurator =>
        {
            configurator.RequireTransportCapability(TransportCapabilities.Scheduling);
            configurator.UsingMediator();
        });
        await using var availableProvider = availableServices.BuildServiceProvider();
        await availableProvider.GetRequiredService<IMessageBus>().StartAsync(CancellationToken.None);

        var nativeServices = new ServiceCollection();
        nativeServices.AddServiceBus(configurator =>
        {
            configurator.RequireTransportCapability(TransportCapabilities.Scheduling, requireNative: true);
            configurator.UsingMediator();
        });
        await using var nativeProvider = nativeServices.BuildServiceProvider();

        await Assert.ThrowsAsync<UnsupportedTransportCapabilityException>(
            () => nativeProvider.GetRequiredService<IMessageBus>().StartAsync(CancellationToken.None));
    }
}
