using System;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class ConsumeContextImpl<TMessage> : BasePipeContext, ConsumeContext<TMessage>
    where TMessage : class
{
    private readonly ReceiveContext receiveContext;
    private readonly ITransportFactory _transportFactory;
    private readonly ISendPipe _sendPipe;
    private readonly IPublishPipe _publishPipe;
    private readonly IMessageSerializer _messageSerializer;
    private TMessage? message;

    public ConsumeContextImpl(ReceiveContext receiveContext, ITransportFactory transportFactory,
        ISendPipe sendPipe, IPublishPipe publishPipe, IMessageSerializer messageSerializer)
        : base(receiveContext.CancellationToken)
    {
        this.receiveContext = receiveContext;
        this._transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _publishPipe = publishPipe;
        _messageSerializer = messageSerializer;
    }

    internal ReceiveContext ReceiveContext => receiveContext;

    public TMessage Message => message is null ? (receiveContext.TryGetMessage(out message) ? message : default) : message;

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        ISendEndpoint endpoint = new TransportSendEndpoint(_transportFactory, _sendPipe, _messageSerializer, uri);
        return Task.FromResult(endpoint);
    }

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(InvalidCastException))]
    public async Task PublishAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        await PublishAsync<T>((object)message!, contextCallback, cancellationToken);
    }

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException))]
    public async Task PublishAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        var exchangeName = NamingConventions.GetExchangeName(typeof(T));

        var uri = new Uri($"rabbitmq://localhost/exchange/{exchangeName}");
        var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);

        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(T)), _messageSerializer, cancellationToken)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        contextCallback?.Invoke(context);

        await _publishPipe.Send(context);
        await _sendPipe.Send(context);
        await transport.Send(message, context, cancellationToken);
    }

    [Throws(typeof(InvalidOperationException))]
    public async Task RespondAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
    {
        await RespondAsync<T>((object)message, contextCallback, cancellationToken);
    }

    [Throws(typeof(InvalidOperationException))]
    public async Task RespondAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
    {
        var address = receiveContext.ResponseAddress ??
                      throw new InvalidOperationException("ResponseAddress not specified");

        var transport = await _transportFactory.GetSendTransport(address, cancellationToken);

        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(T)), _messageSerializer, cancellationToken)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        contextCallback?.Invoke(context);

        await _sendPipe.Send(context);
        await transport.Send(message, context, cancellationToken);
    }

    [Throws(typeof(InvalidOperationException))]
    internal async Task RespondFaultAsync(Exception exception, CancellationToken cancellationToken = default)
    {
        var address = receiveContext.FaultAddress ?? receiveContext.ResponseAddress;
        if (address == null)
            return;

        var fault = new Fault<TMessage>
        {
            Message = Message,
            FaultId = Guid.NewGuid(),
            MessageId = receiveContext.MessageId,
            SentTime = DateTimeOffset.UtcNow,
            Host = GetHostInfo<TMessage>(),
            Exceptions = [ExceptionInfo.FromException(exception)]
        };

        var transport = await _transportFactory.GetSendTransport(address, cancellationToken);
        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(Fault<TMessage>)), _messageSerializer, cancellationToken)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await _sendPipe.Send(context);
        await transport.Send(fault, context, cancellationToken);
    }

    [Throws(typeof(InvalidCastException))]
    public Task Forward<T>(Uri address, T message, CancellationToken cancellationToken = default) where T : class
        => Forward<T>(address, (object)message!, cancellationToken);

    public async Task Forward<T>(Uri address, object message, CancellationToken cancellationToken = default) where T : class
    {
        var endpoint = await GetSendEndpoint(address).ConfigureAwait(false);
        await endpoint.Send<T>(message, null, cancellationToken).ConfigureAwait(false);
    }

    [Throws(typeof(InvalidOperationException))]
    private static HostInfo GetHostInfo<T>() where T : class => new HostInfo
    {
        MachineName = Environment.MachineName,
        ProcessName = Environment.ProcessPath ?? "unknown",
        ProcessId = Environment.ProcessId,
        Assembly = typeof(T).Assembly.GetName().Name ?? "unknown",
        AssemblyVersion = typeof(T).Assembly.GetName().Version?.ToString() ?? "unknown",
        FrameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        MassTransitVersion = "your-custom-version",
        OperatingSystemVersion = Environment.OSVersion.VersionString
    };

}
