using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using System.Linq;

namespace MyServiceBus;

public static class HttpConfiguratorExtensions
{
    [Throws(typeof(InvalidOperationException))]
    public static void ConfigureEndpoints(this IHttpFactoryConfigurator configurator, IBusRegistrationContext context)
    {
        var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
        var formatter = configurator.EndpointNameFormatter;

        foreach (var consumer in registry.Consumers)
        {
            var consumerType = consumer.ConsumerType;
            var messageType = consumer.Bindings.First().MessageType;
            var path = formatter?.Format(messageType) ?? consumer.Address;

            configurator.ReceiveEndpoint(path, [Throws(typeof(AmbiguousMatchException))] (e) =>
            {
                var method = typeof(HttpReceiveEndpointConfigurator)
                    .GetMethod(nameof(HttpReceiveEndpointConfigurator.ConfigureConsumer))!
                    .MakeGenericMethod(consumerType);
                method.Invoke(e, new object[] { context });
            });
        }
    }
}
