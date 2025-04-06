using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

internal sealed class RabbitMqFactoryConfigurator : IRabbitMqFactoryConfigurator
{
    public RabbitMqFactoryConfigurator()
    {
    }

    public string Host { get; private set; } = "localhost";

    public void SetHost(string host)
    {
        Host = host;
    }
}

public static class RabbitMqConfiguratorExtensions
{
    public static void ConfigureEndpoints(this IRabbitMqFactoryConfigurator configurator, IBusRegistrationContext context)
    {
        var registry = context.ServiceProvider.GetRequiredService<IConsumerRegistry>();

        foreach (var consumerType in registry.GetAll())
        {
            var messageType = consumerType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
                ?.GetGenericArguments().First();

            if (messageType == null)
                continue;

            var method = typeof(IMessageBus).GetMethod("AddConsumer")!
                .MakeGenericMethod(messageType, consumerType);

            var bus = context.ServiceProvider.GetRequiredService<IMessageBus>();
            var queueName = NamingHelpers.GetQueueName(consumerType);

            ((Task)method.Invoke(bus, [queueName, CancellationToken.None])).GetAwaiter().GetResult();
        }
    }
}
