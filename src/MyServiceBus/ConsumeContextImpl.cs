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

    public TMessage Message => message is null ? (receiveContext.TryGetMessage(out message) ? message : default) : message;

    public ISendEndpoint GetSendEndpoint(Uri uri)
    {
        return new TransportSendEndpoint(_transportFactory, _sendPipe, _messageSerializer, uri);
    }

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException))]
    public async Task PublishAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
    {
        await PublishAsync<T>((object)message!, contextCallback, cancellationToken);
    }

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException))]
    public async Task PublishAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
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

    class TransportSendEndpoint : ISendEndpoint
    {
        readonly ITransportFactory _transportFactory;
        readonly ISendPipe _sendPipe;
        readonly IMessageSerializer _serializer;
        readonly Uri _address;

        public TransportSendEndpoint(ITransportFactory transportFactory, ISendPipe sendPipe, IMessageSerializer serializer, Uri address)
        {
            _transportFactory = transportFactory;
            _sendPipe = sendPipe;
            _serializer = serializer;
            _address = address;
        }

        [Throws(typeof(InvalidOperationException))]
        public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
            => Send<T>((object)message, contextCallback, cancellationToken);

        [Throws(typeof(InvalidOperationException))]
        public async Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        {
            var transport = await _transportFactory.GetSendTransport(_address, cancellationToken);
            var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(T)), _serializer, cancellationToken)
            {
                MessageId = Guid.NewGuid().ToString()
            };

            contextCallback?.Invoke(context);

            await _sendPipe.Send(context);
            await transport.Send(message, context, cancellationToken);
        }
    }
}
