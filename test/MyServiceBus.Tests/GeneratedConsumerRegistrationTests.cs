using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Generated;
using MyServiceBus.Topology;
using TestApp;

namespace MyServiceBus.Tests;

public class GeneratedConsumerRegistrationTests
{
    [Fact]
    public void Generated_catalog_registers_typed_consumer_descriptors()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);

        configurator.AddGeneratedConsumers();
        configurator.Build();

        using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<TopologyRegistry>();
        var consumers = topology.Consumers.OrderBy(consumer => consumer.ConsumerType.Name).ToArray();

        Assert.Equal(
            ["__MethodConsumer0", "__MethodConsumer1", "__MethodConsumer2", "__MethodConsumer3", "__MethodConsumer4", "__MethodConsumer5", "FulfillmentCompletedConsumer", "FulfillmentRequestedConsumer", "InventoryCheckRequestedConsumer", "InventoryReservedConsumer", "OrderSubmittedConsumer", "ParallelOrderChecksRequestedConsumer", "PaymentCheckRequestedConsumer", "SubmitOrderConsumer", "SubmitOrderFaultConsumer", "TestRequestConsumer"],
            consumers.Select(consumer => consumer.ConsumerType.Name));
        Assert.All(consumers, consumer =>
        {
            Assert.NotNull(consumer.Registration);
            Assert.Equal(consumer.ConsumerType, consumer.Registration.ConsumerType);
            Assert.Equal(consumer.Bindings.Single().MessageType, consumer.Registration.MessageType);
        });

        var methodConsumer = consumers.Single(consumer =>
            consumer.Bindings.Single().MessageType == typeof(GeneratedMethodMessage));
        Assert.Equal("generated-methods", methodConsumer.QueueName);
        Assert.Equal(typeof(GeneratedMethodMessage), methodConsumer.Bindings.Single().MessageType);
        Assert.Equal(
            "test-request-override",
            consumers.Single(consumer => consumer.ConsumerType.Name == "TestRequestConsumer").QueueName);
    }

    [Fact]
    public async Task Generated_response_methods_respond_with_task_and_value_task_results()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(configurator => configurator.AddGeneratedConsumers());
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        await harness.Start();

        var taskResponse = await provider.GetRequiredService<IRequestClient<GeneratedResponseRequest>>()
            .GetResponseAsync<GeneratedResponse>(new GeneratedResponseRequest("task"));
        var valueTaskResponse = await provider.GetRequiredService<IRequestClient<GeneratedValueTaskResponseRequest>>()
            .GetResponseAsync<GeneratedValueTaskResponse>(new GeneratedValueTaskResponseRequest("value-task"));

        Assert.Equal("task-response", taskResponse.Message.Value);
        Assert.Equal("value-task-response", valueTaskResponse.Message.Value);
        await harness.Stop();
    }

    [Fact]
    public async Task Generated_method_consumer_binds_message_context_service_and_cancellation()
    {
        var services = new ServiceCollection();
        var audit = new GeneratedConsumerAudit();
        services.AddSingleton(audit);
        var configurator = new BusRegistrationConfigurator(services);
        configurator.AddGeneratedConsumers();
        configurator.Build();

        await using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<TopologyRegistry>();
        var methodConsumer = topology.Consumers.Single(consumer =>
            consumer.Bindings.Single().MessageType == typeof(GeneratedMethodMessage));
        var instance = Assert.IsAssignableFrom<IConsumer<GeneratedMethodMessage>>(
            provider.GetRequiredService(methodConsumer.ConsumerType));
        using var cancellation = new CancellationTokenSource();
        var message = new GeneratedMethodMessage("generated");
        var context = new DefaultConsumeContext<GeneratedMethodMessage>(message, cancellationToken: cancellation.Token);

        await instance.Consume(context);

        Assert.Same(message, audit.Message);
        Assert.Same(context, audit.Context);
        Assert.Equal(cancellation.Token, audit.CancellationToken);
    }

    [Fact]
    public async Task Generated_static_consumer_classes_group_multiple_consumer_methods()
    {
        var services = new ServiceCollection();
        var audit = new GeneratedConsumerAudit();
        services.AddSingleton(audit);
        var configurator = new BusRegistrationConfigurator(services);
        configurator.AddGeneratedConsumers();
        configurator.Build();

        await using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<TopologyRegistry>();
        var groupedConsumers = topology.Consumers
            .Where(consumer => consumer.QueueName == "generated-methods")
            .ToArray();
        Assert.Equal(2, groupedConsumers.Length);
        Assert.Single(topology.ReceiveEndpoints, endpoint => endpoint.Name == "generated-methods");
        Assert.All(groupedConsumers, consumer => Assert.True(consumer.EndpointNameIsExplicit));
        var methodConsumer = topology.Consumers.Single(consumer => consumer.QueueName == "generated-class-method");
        var instance = Assert.IsAssignableFrom<IConsumer<GeneratedClassMethodMessage>>(
            provider.GetRequiredService(methodConsumer.ConsumerType));
        var message = new GeneratedClassMethodMessage("class");
        var context = new DefaultConsumeContext<GeneratedClassMethodMessage>(message);

        await instance.Consume(context);

        Assert.Same(message, audit.ClassMessage);
        Assert.Same(context, audit.ClassContext);
    }

    [Fact]
    public void Generated_bare_method_attribute_uses_the_method_name_convention()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);
        configurator.AddGeneratedConsumers();
        configurator.Build();

        using var provider = services.BuildServiceProvider();
        var topology = provider.GetRequiredService<TopologyRegistry>();
        var consumer = topology.Consumers.Single(candidate =>
            candidate.Bindings.Single().MessageType == typeof(GeneratedConventionMethodMessage));

        Assert.Equal(nameof(MethodAttributedConsumers.ObserveConvention), consumer.QueueName);
        Assert.False(consumer.EndpointNameIsExplicit);
        Assert.Null(consumer.EndpointNameFormatterType);
    }
}
