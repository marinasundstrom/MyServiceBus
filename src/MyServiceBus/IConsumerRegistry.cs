namespace MyServiceBus;

public interface IConsumerRegistry
{
    void Register(Type consumerType);
    IReadOnlyList<Type> GetAll();
}
