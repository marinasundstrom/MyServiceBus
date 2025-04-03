namespace MyServiceBus;

public interface MessageConsumeContext
{
    Task Respond<T>(object message, CancellationToken cancellationToken = default);

    Task Respond<T>(T message, CancellationToken cancellationToken = default);

}
