namespace MyServiceBus;

public interface ISendEndpoint
{
    Task Send<T>(object message, CancellationToken cancellationToken = default);

    Task Send<T>(T message, CancellationToken cancellationToken = default);
}
