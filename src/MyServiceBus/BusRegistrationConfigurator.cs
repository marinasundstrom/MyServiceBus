using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

namespace MyServiceBus;

public class BusRegistrationConfigurator : IBusRegistrationConfigurator
{
    private TopologyRegistry _topology = new TopologyRegistry();

    public IServiceCollection Services { get; }

    public BusRegistrationConfigurator(IServiceCollection services)
    {
        Services = services;
    }

    public void AddConsumer<TConsumer>() where TConsumer : class, IConsumer
    {
        Services.AddScoped<TConsumer>();
        Services.AddScoped<IConsumer, TConsumer>();

        _topology.RegisterConsumer<TConsumer>(
          queueName: NamingHelpers.GetQueueName(typeof(TConsumer)),
          messageTypes: GetHandledMessageTypes(typeof(TConsumer))
      );
    }

    private static Type[] GetHandledMessageTypes(Type consumerType)
    {
        return consumerType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToArray();
    }

    public void Build()
    {
        Services.AddSingleton<TopologyRegistry>(_topology);

        /*
        Services.AddSingleton(provider =>
        {
            var bus = new MyMessageBus(_topology); // swap with actual bus later
            var cfg = new RabbitMQ (bus); // replace with real one
            _rabbitConfig?.Invoke(provider, cfg);
            return bus;
        });
        */
    }
}
