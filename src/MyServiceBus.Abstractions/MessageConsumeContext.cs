namespace MyServiceBus;

public interface MessageConsumeContext
{
    Task RespondAsync<T>(T message, CancellationToken cancellationToken = default);
}
