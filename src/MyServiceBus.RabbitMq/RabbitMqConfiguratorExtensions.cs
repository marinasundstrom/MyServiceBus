namespace MyServiceBus;

using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

public static class RabbitMqConfiguratorExtensions
{
    public static void ConfigureEndpoints(this IRabbitMqFactoryConfigurator configurator, IBusRegistrationContext context)
    {
        var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
        foreach (var consumer in registry.Consumers)
        {
            var queueName = consumer.ResolveEndpointName(configurator.EndpointNameFormatter);

            configurator.ReceiveEndpoint(queueName, endpoint => endpoint.ConfigureConsumer(context, consumer));
        }
    }
}
