

using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IRabbitMqFactoryConfigurator
{
    void Message<T>(Action<MessageConfigurator> configure);
    void ReceiveEndpoint(string queueName, Action<ReceiveEndpointConfigurator> configure);
    void Host(string host, Action<IRabbitMqHostConfigurator>? configure = null);
}

public interface IRabbitMqHostConfigurator
{
    void Username(string username);
    void Password(string password);
}

public class MessageConfigurator
{
    public void SetEntityName(string name)
    {

    }
}

public class ReceiveEndpointConfigurator : IReceiveEndpointConfigurator
{
    [Throws(typeof(InvalidOperationException))]
    public void ConfigureConsumer<T>(IBusRegistrationContext context)
    {
        var consumerType = typeof(T);

        try
        {
            var messageType = consumerType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
                ?.GetGenericArguments().First();

            if (messageType == null)
                return;

            var bus = context.ServiceProvider.GetRequiredService<IMessageBus>();
            var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
            var consumer = registry.Consumers.First(c => c.ConsumerType == consumerType);

            var method = typeof(IMessageBus).GetMethod("AddConsumer")!
                .MakeGenericMethod(messageType, consumerType);

            ((Task)method.Invoke(bus, new object[] { consumer, CancellationToken.None }))
                .GetAwaiter().GetResult();
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}", ex.InnerException);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}", ex);
        }
    }
}