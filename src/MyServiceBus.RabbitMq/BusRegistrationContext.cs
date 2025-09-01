


using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using System.Reflection;

namespace MyServiceBus;

internal sealed class BusRegistrationContext : IBusRegistrationContext
{
    public BusRegistrationContext(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    [Throws(typeof(InvalidOperationException))]
    public void ConfigureEndpoints<T>(IReceiveConfigurator<T> configurator, IEndpointNameFormatter endpointNameFormatter = null)
            where T : IReceiveEndpointConfigurator
    {
        var registry = ServiceProvider.GetRequiredService<TopologyRegistry>();

        if (configurator is not IRabbitMqFactoryConfigurator rabbitConfigurator)
            throw new InvalidOperationException("Configurator must be a RabbitMQ factory configurator");

        foreach (var consumer in registry.Consumers)
        {
            var consumerType = consumer.ConsumerType;

            try
            {
                rabbitConfigurator.ReceiveEndpoint(consumer.QueueName, endpoint =>
                {
                    var method = typeof(ReceiveEndpointConfigurator)
                        .GetMethod("ConfigureConsumer")!
                        .MakeGenericMethod(consumerType);

                    method.Invoke(endpoint, new object[] { this });
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

public interface IReceiveEndpointConfigurator
{
}

public interface IEndpointNameFormatter
{
}

public interface IReceiveConfigurator<T>
{
}