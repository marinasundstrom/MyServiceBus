namespace MyServiceBus;

public class ConsumerRegistry : IConsumerRegistry
{
    private readonly List<Type> _consumers = new();

    public void Register(Type consumerType)
    {
        _consumers.Add(consumerType);
    }

    public IReadOnlyList<Type> GetAll() => _consumers;
}