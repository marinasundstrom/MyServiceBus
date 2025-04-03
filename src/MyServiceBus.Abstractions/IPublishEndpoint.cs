namespace MyServiceBus;

public interface IPublishEndpoint
{
    Task Publish<T>(object message, CancellationToken cancellationToken = default);

    Task Publish<T>(T message, CancellationToken cancellationToken = default);
}
