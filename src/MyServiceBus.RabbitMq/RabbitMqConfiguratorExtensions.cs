namespace MyServiceBus;

using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using System.Reflection;

public static class RabbitMqConfiguratorExtensions
{
    [Throws(typeof(InvalidOperationException))]
    public static void ConfigureEndpoints(this IRabbitMqFactoryConfigurator configurator, IBusRegistrationContext context)
    {
        var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();

        foreach (var consumer in registry.Consumers)
        {
            var consumerType = consumer.ConsumerType;

            try
            {
                configurator.ReceiveEndpoint(consumer.QueueName, endpoint =>
                {
                    var method = typeof(ReceiveEndpointConfigurator)
                        .GetMethod("ConfigureConsumer")!
                        .MakeGenericMethod(consumerType);

                    method.Invoke(endpoint, new object[] { context });
                });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException($"Failed to configure endpoint for {consumerType.Name}", ex.InnerException);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to configure endpoint for {consumerType.Name}", ex);
            }
        }
    }
}
