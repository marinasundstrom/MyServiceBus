using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

namespace MyServiceBus.Tests;

public class ConsumerDefinitionTests
{
    [Fact]
    public void Applies_definition_before_materializing_consumer_topology()
    {
        var services = new ServiceCollection();
        var definition = new DefinedConsumerDefinition();
        services.AddServiceBusTestHarness(configurator =>
            configurator.AddConsumer<DefinedConsumer>(definition));
        definition.EndpointName = "changed-after-registration";
        definition.ConcurrentMessageLimit = 9;
        definition.PrefetchCount = 10;

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TopologyRegistry>();
        var consumer = Assert.Single(registry.Consumers);

        Assert.Equal("defined-orders", consumer.QueueName);
        Assert.True(consumer.EndpointNameIsExplicit);
        Assert.Equal(7, consumer.ConcurrentMessageLimit);
        Assert.Equal((ushort)11, consumer.PrefetchCount);
        var model = Assert.Single(registry.ConsumerDefinitions);
        Assert.Equal(typeof(DefinedConsumer), model.ConsumerType);
        Assert.Equal("defined-orders", model.EndpointName);
        Assert.True(model.EndpointNameIsExplicit);
        Assert.Equal(typeof(DefinedConsumer), model.EndpointNameFormatterType);
        Assert.Equal([typeof(SubmitOrder)], model.MessageTypes);
        Assert.Equal(7, model.ConcurrentMessageLimit);
        Assert.Equal((ushort)11, model.Endpoint.PrefetchCount);
        Assert.Same(model, consumer.Definition);
    }

    [Fact]
    public void Rejects_invalid_definition_values()
    {
        var definition = new ConsumerDefinition<DefinedConsumer>();

        Assert.Throws<ArgumentException>(() => definition.EndpointName = " ");
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConcurrentMessageLimit = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.PrefetchCount = 0);
    }

    [Fact]
    public void Inline_configuration_builds_the_same_definition_model()
    {
        var services = new ServiceCollection();
        IConsumerRegistrationConfigurator<InlineConsumer>? registration = null;
        services.AddServiceBusTestHarness(configurator =>
            registration = configurator.AddConsumer<InlineConsumer>(definition =>
            {
                definition.EndpointName = "inline-orders";
                definition.ConcurrentMessageLimit = 3;
            }));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TopologyRegistry>();
        var consumer = Assert.Single(registry.Consumers);

        Assert.Equal("inline-orders", consumer.QueueName);
        Assert.Equal(3, consumer.ConcurrentMessageLimit);
        var model = Assert.Single(registry.ConsumerDefinitions);
        Assert.Equal(typeof(InlineConsumer), model.ConsumerType);
        Assert.Equal("inline-orders", model.EndpointName);
        Assert.Equal(3, model.ConcurrentMessageLimit);
        Assert.Equal(model, Assert.IsType<ConsumerDefinitionModel>(registration?.Definition));
    }

    [Fact]
    public void Composed_endpoint_definition_is_snapshotted_at_registration()
    {
        var endpoint = new EndpointDefinition
        {
            Name = "shared-orders",
            ConcurrentMessageLimit = 5,
            PrefetchCount = 13
        };
        var definition = new ConsumerDefinition<InlineConsumer>(endpoint);
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(configurator => configurator.AddConsumer(definition));
        endpoint.Name = "changed";
        endpoint.PrefetchCount = 21;

        using var provider = services.BuildServiceProvider();
        var model = Assert.Single(provider.GetRequiredService<TopologyRegistry>().ConsumerDefinitions);

        Assert.Equal("shared-orders", model.Endpoint.Name);
        Assert.Equal(5, model.Endpoint.ConcurrentMessageLimit);
        Assert.Equal((ushort)13, model.Endpoint.PrefetchCount);
    }

    [Fact]
    public void Captures_resolved_metadata_for_every_consumer_registration_shape()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(configurator =>
        {
            configurator.AddConsumer<MultiContractConsumer>();
            configurator.AddConsumer<TypedConsumer, SubmitOrder>();
        });

        using var provider = services.BuildServiceProvider();
        IBusTopology topology = provider.GetRequiredService<TopologyRegistry>();

        var multiContract = Assert.Single(
            topology.ConsumerDefinitions,
            definition => definition.ConsumerType == typeof(MultiContractConsumer));
        Assert.False(multiContract.EndpointNameIsExplicit);
        Assert.Equal(typeof(MultiContractConsumer), multiContract.EndpointNameFormatterType);
        Assert.Equal(
            [typeof(SubmitOrder), typeof(CancelOrder)],
            multiContract.MessageTypes);

        var typed = Assert.Single(
            topology.ConsumerDefinitions,
            definition => definition.ConsumerType == typeof(TypedConsumer));
        Assert.Equal([typeof(SubmitOrder)], typed.MessageTypes);
        Assert.Same(
            typed,
            Assert.Single(topology.Consumers, consumer => consumer.ConsumerType == typeof(TypedConsumer)).Definition);
    }

    private sealed record SubmitOrder(Guid OrderId);

    private sealed record CancelOrder(Guid OrderId);

    private sealed class DefinedConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    private sealed class InlineConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    private sealed class MultiContractConsumer : IConsumer<SubmitOrder>, IConsumer<CancelOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;

        public Task Consume(ConsumeContext<CancelOrder> context) => Task.CompletedTask;
    }

    private sealed class TypedConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    private sealed class DefinedConsumerDefinition : ConsumerDefinition<DefinedConsumer>
    {
        public DefinedConsumerDefinition()
        {
            EndpointName = "defined-orders";
            ConcurrentMessageLimit = 7;
            PrefetchCount = 11;
        }
    }
}
