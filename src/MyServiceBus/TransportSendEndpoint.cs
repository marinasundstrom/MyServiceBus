using System;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Serialization;

namespace MyServiceBus;

internal class TransportSendEndpoint : ISendEndpoint
{
    readonly ITransportFactory _transportFactory;
    readonly ISendPipe _sendPipe;
    readonly IMessageSerializer _serializer;
    readonly Uri _address;
    readonly Uri _sourceAddress;
    readonly ISendContextFactory _contextFactory;

    public TransportSendEndpoint(ITransportFactory transportFactory, ISendPipe sendPipe, IMessageSerializer serializer, Uri address, Uri sourceAddress, ISendContextFactory contextFactory)
    {
        _transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _serializer = serializer;
        _address = address;
        _sourceAddress = sourceAddress;
        _contextFactory = contextFactory;
    }

    [Throws(typeof(InvalidOperationException))]
    public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        => Send<T>((object)message!, contextCallback, cancellationToken);

    [Throws(typeof(InvalidOperationException))]
    public async Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
    {
        var transport = await _transportFactory.GetSendTransport(_address, cancellationToken);
        var context = _contextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), _serializer, cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = _sourceAddress;
        context.DestinationAddress = _address;

        contextCallback?.Invoke(context);

        await _sendPipe.Send(context);
        await transport.Send(message, context, cancellationToken);
    }
}
