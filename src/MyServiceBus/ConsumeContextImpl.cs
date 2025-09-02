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
    private TMessage? message;

    public ConsumeContextImpl(ReceiveContext receiveContext, ITransportFactory transportFactory,
        ISendPipe sendPipe, IPublishPipe publishPipe)
        : base(receiveContext.CancellationToken)
    {
        this.receiveContext = receiveContext;
        this._transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _publishPipe = publishPipe;
    }

    public TMessage Message => message is null ? (receiveContext.TryGetMessage(out message) ? message : default) : message;

    public ISendEndpoint GetSendEndpoint(Uri uri)
    {
        return new TransportSendEndpoint(_transportFactory, _sendPipe, uri);
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

        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(T)), new EnvelopeMessageSerializer(), cancellationToken)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await _publishPipe.Send(context);
        await _sendPipe.Send(context);
        await transport.Send(message, context, cancellationToken);
    }

    public async Task RespondAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        await RespondAsync<T>((object)message, cancellationToken);
    }

    public async Task RespondAsync<T>(object message, CancellationToken cancellationToken = default)
    {
        var address = receiveContext.ResponseAddress ??
                      throw new InvalidOperationException("ResponseAddress not specified");

        var transport = await _transportFactory.GetSendTransport(address, cancellationToken);

        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(T)), new EnvelopeMessageSerializer(), cancellationToken)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await _sendPipe.Send(context);
        await transport.Send(message, context, cancellationToken);
    }

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
        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(Fault<TMessage>)), new EnvelopeMessageSerializer(), cancellationToken)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        await _sendPipe.Send(context);
        await transport.Send(fault, context, cancellationToken);
    }

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
        readonly Uri _address;

        public TransportSendEndpoint(ITransportFactory transportFactory, ISendPipe sendPipe, Uri address)
        {
            _transportFactory = transportFactory;
            _sendPipe = sendPipe;
            _address = address;
        }

        public Task Send<T>(T message, CancellationToken cancellationToken = default)
            => Send<T>((object)message, cancellationToken);

        public async Task Send<T>(object message, CancellationToken cancellationToken = default)
        {
            var transport = await _transportFactory.GetSendTransport(_address, cancellationToken);
            var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(T)), new EnvelopeMessageSerializer(), cancellationToken)
            {
                MessageId = Guid.NewGuid().ToString()
            };

            await _sendPipe.Send(context);
            await transport.Send(message, context, cancellationToken);
        }
    }
}
