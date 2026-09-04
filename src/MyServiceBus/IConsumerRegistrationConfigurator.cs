namespace MyServiceBus;

public interface IConsumerRegistrationConfigurator<T> where T : class, IConsumer
{
    Topology.ConsumerDefinitionModel Definition { get; }
}
