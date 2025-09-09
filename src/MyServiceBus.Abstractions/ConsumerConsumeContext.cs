namespace MyServiceBus;

public interface ConsumerConsumeContext<out TConsumer, out TMessage> : ConsumeContext<TMessage>
    where TConsumer : class
    where TMessage : class
{
    TConsumer Consumer { get; }
}
