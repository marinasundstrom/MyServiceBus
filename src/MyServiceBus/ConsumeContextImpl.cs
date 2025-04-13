
using MyServiceBus.Serialization;

namespace MyServiceBus;

internal class ConsumeContextImpl<TMessage> : ConsumeContext<TMessage>
    where TMessage : class
{
    private readonly ReceiveContext receiveContext;
    private readonly ITransportFactory _transportFactory;
    private TMessage? message;

    public ConsumeContextImpl(ReceiveContext receiveContext, ITransportFactory transportFactory)
    {
        this.receiveContext = receiveContext;
        this._transportFactory = transportFactory;
    }

    public CancellationToken CancellationToken => CancellationToken.None;

    public TMessage Message => message is null ? (receiveContext.TryGetMessage(out message) ? message : default) : message;

    public ISendEndpoint GetSendEndpoint(Uri uri)
    {
        throw new NotImplementedException();
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        await PublishAsync((object)message, cancellationToken);
    }

    public async Task PublishAsync<T>(object message, CancellationToken cancellationToken = default)
    {
        var exchangeName = NamingConventions.GetExchangeName(typeof(T));

        var uri = new Uri($"rabbitmq://localhost/{exchangeName}");
        var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = new SendContext([typeof(T)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await transport.Send(message, context, cancellationToken);
    }

    public async Task RespondAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        await RespondAsync((object)message, cancellationToken);
    }

    private async Task RespondAsync<T>(object obj, CancellationToken cancellationToken = default)
    {
        var responseAddress = receiveContext.ResponseAddress;

        var exchangeName = NamingConventions.GetExchangeName(typeof(T));

        var uri = new Uri($"rabbitmq://localhost/{exchangeName}");
        var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = new SendContext([typeof(T)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await transport.Send(message, context, cancellationToken);
    }
}
