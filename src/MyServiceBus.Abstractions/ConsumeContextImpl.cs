
namespace MyServiceBus;

internal class ConsumeContextImpl<TMessage> : ConsumeContext<TMessage>
    where TMessage : class
{
    public TMessage Message => throw new NotImplementedException();

    public CancellationToken CancellationToken => throw new NotImplementedException();

    public ISendEndpoint GetSendEndpoint(Uri uri)
    {
        throw new NotImplementedException();
    }

    public Task Publish<T>(object message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task Publish<T>(T message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task Respond<T>(T message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task Respond<T>(object message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
