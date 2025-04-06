namespace MyServiceBus;

public interface IReceiveTransport
{
    Task Start(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
}
