using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

namespace MyServiceBus;

public static class RabbitMqConfiguratorExtensions
{
    /*
    public static void ConfigureEndpoints(this IRabbitMqFactoryConfigurator configurator, IBusRegistrationContext context)
    {
        var topology = context.ServiceProvider.GetRequiredService<TopologyRegistry>();

        foreach (var consumer in topology.Consumers)
        {
            Console.WriteLine($"Declaring queue: {consumer.QueueName}");
            foreach (var binding in consumer.Bindings)
            {
                Console.WriteLine($"  → Binding to exchange: {binding.EntityName} for {binding.MessageType.Name}");
            }
        }
    }
    */

    public static void ConfigureEndpoints(this IRabbitMqFactoryConfigurator configurator, IBusRegistrationContext context)
    {
        var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();

        foreach (var consumer in registry.Consumers)
        {
            var consumerType = consumer.ConsumerType;

            var messageType = consumerType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
                ?.GetGenericArguments().First();

            if (messageType == null)
                continue;

            var method = typeof(IMessageBus).GetMethod("AddConsumer")!
                .MakeGenericMethod(messageType, consumerType);

            var bus = context.ServiceProvider.GetRequiredService<IMessageBus>();

            var queueName = NamingConventions.GetQueueName(consumerType);

            ((Task)method.Invoke(bus, [consumer, CancellationToken.None])).GetAwaiter().GetResult();
        }
    }
}
