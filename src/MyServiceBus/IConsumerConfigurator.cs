namespace MyServiceBus;

public interface IConsumerConfigurator<T> where T : class, IConsumer
{
    string? EndpointName { get; set; }

    int? ConcurrentMessageLimit { get; set; }

    ushort? PrefetchCount { get; set; }
}
