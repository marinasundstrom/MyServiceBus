
namespace MyServiceBus;

internal class ConsumeContextImpl<TMessage> : ConsumeContext<TMessage>
    where TMessage : class
{
    private readonly ReceiveContext receiveContext;
    private TMessage? message;

    public ConsumeContextImpl(ReceiveContext receiveContext)
    {
        this.receiveContext = receiveContext;
    }

    public CancellationToken CancellationToken => CancellationToken.None;

    public TMessage Message => message is null ? (receiveContext.TryGetMessage(out message) ? message : default) : message;

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
