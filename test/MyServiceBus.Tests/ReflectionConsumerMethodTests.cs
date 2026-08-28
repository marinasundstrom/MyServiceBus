using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestApp;

namespace MyServiceBus.Tests;

public class ReflectionConsumerMethodTests
{
    [Fact]
    public async Task Reflection_discovery_binds_method_message_context_services_and_cancellation()
    {
        var services = new ServiceCollection();
        var audit = new GeneratedConsumerAudit();
        services.AddSingleton(audit);
        services.AddServiceBus(configurator =>
        {
            configurator.UsingMediator();
            configurator.AddConsumers(typeof(GeneratedMethodConsumers).Assembly);
        });

        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var message = new GeneratedMethodMessage("reflection");

        await provider.GetRequiredService<IMessageBus>().Publish(message, cancellationToken: cancellation.Token);

        var topology = provider.GetRequiredService<MyServiceBus.Topology.TopologyRegistry>();
        Assert.Contains(topology.Consumers, consumer => consumer.QueueName == "generated-methods");
        Assert.Contains(topology.Consumers, consumer => consumer.QueueName == "generated-class-method");
        Assert.Contains(topology.Consumers, consumer => consumer.QueueName == "test-request-override");
        Assert.Same(message, audit.Message);
        Assert.Same(message, audit.Context?.Message);
        Assert.Equal(audit.Context?.CancellationToken ?? default, audit.CancellationToken);
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Consumer_method_class_can_use_fluent_endpoint_mapping_without_an_attribute()
    {
        var services = new ServiceCollection();
        var audit = new GeneratedConsumerAudit();
        services.AddSingleton(audit);
        services.AddServiceBus(configurator =>
        {
            configurator.UsingMediator();
            configurator.AddConsumerMethods<FluentMethodConsumer>("fluent-class-method");
        });

        await using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<MyServiceBus.Topology.TopologyRegistry>();
        Assert.Equal("fluent-class-method", topology.Consumers.Single().QueueName);
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);
        var message = new GeneratedClassMethodMessage("marker");

        await provider.GetRequiredService<IMessageBus>().Publish(message);

        Assert.Same(message, audit.ClassMessage);
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Assembly_discovery_can_filter_types_and_find_a_method_attribute_without_a_class_attribute()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);

        configurator.AddConsumers(
            type => type == typeof(MethodAttributedConsumers),
            typeof(MethodAttributedConsumers).Assembly);
        configurator.Build();

        using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<MyServiceBus.Topology.TopologyRegistry>();
        var consumer = topology.Consumers.Single(candidate =>
            candidate.Bindings.Single().MessageType == typeof(GeneratedClassMethodMessage));
        Assert.Equal("generated-class-method", consumer.QueueName);
        Assert.Equal(typeof(GeneratedClassMethodMessage), consumer.Bindings.Single().MessageType);
    }

    [Fact]
    public void Endpoint_precedence_is_fluent_then_method_then_class_then_convention()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);

        configurator.AddConsumerMethods(typeof(GroupedMethodConsumers));
        configurator.AddConsumerMethods(typeof(ConventionMethodConsumers));
        configurator.Build();

        using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<MyServiceBus.Topology.TopologyRegistry>();
        Assert.Equal(
            "class-orders",
            topology.Consumers.Single(consumer => consumer.Bindings.Single().MessageType == typeof(ClassMappedMessage)).QueueName);
        Assert.Equal(
            "method-orders",
            topology.Consumers.Single(consumer => consumer.Bindings.Single().MessageType == typeof(MethodMappedMessage)).QueueName);
        Assert.Equal(
            nameof(ConventionMethodConsumers.ObserveConvention),
            topology.Consumers.Single(consumer => consumer.Bindings.Single().MessageType == typeof(ConventionMappedMessage)).QueueName);
    }

    [Fact]
    public void Fluent_endpoint_overrides_class_and_method_attributes_for_the_selected_container()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);

        configurator.AddConsumerMethods(typeof(GroupedMethodConsumers), "fluent-orders");
        configurator.Build();

        using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<MyServiceBus.Topology.TopologyRegistry>();
        Assert.Equal(2, topology.Consumers.Count);
        Assert.All(topology.Consumers, consumer => Assert.Equal("fluent-orders", consumer.QueueName));
        Assert.Single(topology.ReceiveEndpoints);
    }

    private sealed class FluentMethodConsumer
    {
        private readonly GeneratedConsumerAudit audit;

        public FluentMethodConsumer(GeneratedConsumerAudit audit)
        {
            this.audit = audit;
        }

        public Task Receive(
            GeneratedClassMethodMessage message,
            ConsumeContext<GeneratedClassMethodMessage> context)
        {
            audit.RecordClass(message, context);
            return Task.CompletedTask;
        }
    }

    private sealed record ClassMappedMessage;
    private sealed record MethodMappedMessage;
    private sealed record ConventionMappedMessage;

    [Consumer("class-orders")]
    private static class GroupedMethodConsumers
    {
        public static void ObserveClass(ClassMappedMessage message)
        {
        }

        [Consumer("method-orders")]
        public static void ObserveMethod(MethodMappedMessage message)
        {
        }
    }

    private static class ConventionMethodConsumers
    {
        [Consumer]
        public static void ObserveConvention(ConventionMappedMessage message)
        {
        }
    }
}
