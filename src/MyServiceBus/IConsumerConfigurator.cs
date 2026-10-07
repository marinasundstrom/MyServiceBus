namespace MyServiceBus;

public interface IConsumerConfigurator<T> where T : class, IConsumer
{
    void ConfigureMessage<TMessage>(Action<PipeConfigurator<ConsumeContext<TMessage>>> configure) where TMessage : class;

    string? EndpointName { get; set; }

    int? ConcurrentMessageLimit { get; set; }

    ushort? PrefetchCount { get; set; }
}
