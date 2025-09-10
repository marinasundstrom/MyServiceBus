using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    readonly ILogger<TransportSendEndpoint>? _logger;

    public TransportSendEndpoint(ITransportFactory transportFactory, ISendPipe sendPipe, IMessageSerializer serializer, Uri address, Uri sourceAddress, ISendContextFactory contextFactory, ILogger<TransportSendEndpoint>? logger = null)
    {
        _transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _serializer = serializer;
        _address = address;
        _sourceAddress = sourceAddress;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    [Throws(typeof(InvalidOperationException))]
    public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
        => Send<T>((object)message!, contextCallback, cancellationToken);

    [Throws(typeof(InvalidOperationException))]
    public async Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        _logger?.LogDebug("Sending {MessageType} to {DestinationAddress}", typeof(T).Name, _address);

        var transport = await _transportFactory.GetSendTransport(_address, cancellationToken);
        var context = _contextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), _serializer, cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = _sourceAddress;
        context.DestinationAddress = _address;

        contextCallback?.Invoke(context);

        if (context.ScheduledEnqueueTime is DateTime scheduled)
        {
            var delay = scheduled - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        await _sendPipe.Send(context);
        var typed = message is T t ? t : (T)MessageProxy.Create(typeof(T), message);
        await transport.Send(typed, context, cancellationToken);
    }
}
