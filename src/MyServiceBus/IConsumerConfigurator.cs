namespace MyServiceBus;

public interface IConsumerConfigurator<T> where T : class, IConsumer
{
}
