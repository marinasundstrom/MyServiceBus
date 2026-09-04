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

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TopologyRegistry>();
        var consumer = Assert.Single(registry.Consumers);

        Assert.Equal("defined-orders", consumer.QueueName);
        Assert.True(consumer.EndpointNameIsExplicit);
        Assert.Equal(7, consumer.ConcurrentMessageLimit);
        var model = Assert.Single(registry.ConsumerDefinitions);
        Assert.Equal(typeof(DefinedConsumer), model.ConsumerType);
        Assert.Equal("defined-orders", model.EndpointName);
        Assert.Equal(7, model.ConcurrentMessageLimit);
    }

    [Fact]
    public void Rejects_invalid_definition_values()
    {
        var definition = new ConsumerDefinition<DefinedConsumer>();

        Assert.Throws<ArgumentException>(() => definition.EndpointName = " ");
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ConcurrentMessageLimit = 0);
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

    private sealed record SubmitOrder(Guid OrderId);

    private sealed class DefinedConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    private sealed class InlineConsumer : IConsumer<SubmitOrder>
    {
        public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
    }

    private sealed class DefinedConsumerDefinition : ConsumerDefinition<DefinedConsumer>
    {
        public DefinedConsumerDefinition()
        {
            EndpointName = "defined-orders";
            ConcurrentMessageLimit = 7;
        }
    }
}
