
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

    public async Task Publish<T>(T message, CancellationToken cancellationToken = default)
    {
        await Publish((object)message, cancellationToken);
    }

    public async Task Publish<T>(object message, CancellationToken cancellationToken = default)
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

    public async Task Respond<T>(T message, CancellationToken cancellationToken = default)
    {
        await Respond((object)message, cancellationToken);
    }

    public async Task Respond<T>(object message, CancellationToken cancellationToken = default)
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
