namespace MyServiceBus;

public interface ISendTransport
{
    Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
        where T : class;
}
