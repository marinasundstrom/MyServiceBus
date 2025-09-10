using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly Uri _address;
    private readonly ISendContextFactory _sendContextFactory;
    private readonly IPublishContextFactory _publishContextFactory;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IJobScheduler _jobScheduler;
    private TMessage? message;

    public ConsumeContextImpl(ReceiveContext receiveContext, ITransportFactory transportFactory,
        ISendPipe sendPipe, IPublishPipe publishPipe, IMessageSerializer messageSerializer, Uri address,
        ISendContextFactory sendContextFactory, IPublishContextFactory publishContextFactory,
        ILoggerFactory? loggerFactory = null, IJobScheduler? jobScheduler = null)
        : base(receiveContext.CancellationToken)
    {
        this.receiveContext = receiveContext;
        this._transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _publishPipe = publishPipe;
        _messageSerializer = messageSerializer;
        _address = address;
        _sendContextFactory = sendContextFactory;
        _publishContextFactory = publishContextFactory;
        _loggerFactory = loggerFactory;
        _jobScheduler = jobScheduler ?? new DefaultJobScheduler();
    }

    internal ReceiveContext ReceiveContext => receiveContext;

    public TMessage Message => message is null ? (receiveContext.TryGetMessage(out message) ? message : default) : message;

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        var logger = _loggerFactory?.CreateLogger<TransportSendEndpoint>();
        ISendEndpoint endpoint = new TransportSendEndpoint(_transportFactory, _sendPipe, _messageSerializer, uri, _address, _sendContextFactory, logger, _jobScheduler);
        return Task.FromResult(endpoint);
    }

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(InvalidCastException), typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public async Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        await Publish<T>((object)message!, contextCallback, cancellationToken);
    }

    [Throws(typeof(UriFormatException), typeof(InvalidOperationException), typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public async Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        var exchangeName = EntityNameFormatter.Format(typeof(T));

        var uri = new Uri(_address, $"exchange/{exchangeName}");

        var context = _publishContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), _messageSerializer, cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = _address;
        context.DestinationAddress = uri;
        context.RoutingKey = exchangeName;

        contextCallback?.Invoke(context);
        if (context.ScheduledEnqueueTime is DateTime scheduled)
        {
            await _jobScheduler.Schedule(scheduled, async ct =>
            {
                context.ScheduledEnqueueTime = null;
                var transport = await _transportFactory.GetSendTransport(uri, ct);
                await _publishPipe.Send(context);
                await _sendPipe.Send(context);
                var typed = message is T mt ? mt : (T)MessageProxy.Create(typeof(T), message);
                await transport.Send(typed, context, ct);
            }, cancellationToken);
        }
        else
        {
            var transport = await _transportFactory.GetSendTransport(uri, cancellationToken);
            await _publishPipe.Send(context);
            await _sendPipe.Send(context);
            var typed = message is T t ? t : (T)MessageProxy.Create(typeof(T), message);
            await transport.Send(typed, context, cancellationToken);
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public async Task RespondAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        await RespondAsync<T>((object)message!, contextCallback, cancellationToken);
    }

    [Throws(typeof(InvalidOperationException))]
    public async Task RespondAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        var address = receiveContext.ResponseAddress ??
                      throw new InvalidOperationException("ResponseAddress not specified");

        var transport = await _transportFactory.GetSendTransport(address, cancellationToken);

        var context = _sendContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), _messageSerializer, cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = _address;
        context.DestinationAddress = address;

        contextCallback?.Invoke(context);

        await _sendPipe.Send(context);
        var typed = message is T t ? t : (T)MessageProxy.Create(typeof(T), message);
        await transport.Send(typed, context, cancellationToken);
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
        var context = _sendContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(Fault<TMessage>)), _messageSerializer, cancellationToken);
        context.MessageId = Guid.NewGuid().ToString();
        context.SourceAddress = _address;
        context.DestinationAddress = address;

        await _sendPipe.Send(context);
        await transport.Send(fault, context, cancellationToken);
    }

    [Throws(typeof(InvalidCastException))]
    public Task Send<T>(Uri address, T message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class
        => Send<T>(address, (object)message!, contextCallback, cancellationToken);

    public async Task Send<T>(Uri address, object message, Action<ISendContext>? contextCallback = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var endpoint = await GetSendEndpoint(address).ConfigureAwait(false);
        var typed = message is T t ? t : (T)MessageProxy.Create(typeof(T), message);
        await endpoint.Send<T>(typed, contextCallback, cancellationToken).ConfigureAwait(false);
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
