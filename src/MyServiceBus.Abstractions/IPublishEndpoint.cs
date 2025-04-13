namespace MyServiceBus;

public interface IPublishEndpoint
{
    Task PublishAsync<T>(object message, CancellationToken cancellationToken = default);

    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
}
