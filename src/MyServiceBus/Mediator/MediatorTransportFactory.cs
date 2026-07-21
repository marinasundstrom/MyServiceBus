using System.Collections.Concurrent;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

/// <summary>
/// In-memory transport factory used by the Mediator implementation.
/// Messages are dispatched to registered handlers without leaving the process.
/// </summary>
public class MediatorTransportFactory : ITransportFactory
{
    private readonly ConcurrentDictionary<string, List<Func<ReceiveContext, Task>>> _handlers = new();

    public TransportCapabilityDescriptor Capabilities => TransportCapabilityDescriptors.InMemory;

    public Uri GetPublishAddress(string entityName) =>
        new($"loopback://localhost/exchange/{entityName}");

    public Uri GetTemporaryEndpointAddress(string endpointName) =>
        new($"loopback://localhost/exchange/{endpointName}?durable=false&autodelete=true");

    public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
    {
        var exchange = ExtractExchange(address);
        return Task.FromResult<ISendTransport>(new MediatorSendTransport(this, exchange));
    }

    public Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        var transport = new MediatorReceiveTransport(this, topology.ExchangeName, handler);
        return Task.FromResult<IReceiveTransport>(transport);
    }

    internal Task Dispatch(string exchange, ReceiveContext context)
        => Dispatch(new[] { exchange }, context);

    internal Task Dispatch(IEnumerable<string> exchanges, ReceiveContext context)
    {
        var matchingHandlers = new HashSet<Func<ReceiveContext, Task>>();
        foreach (var exchange in exchanges.Distinct(StringComparer.Ordinal))
        {
            if (!_handlers.TryGetValue(exchange, out var handlers))
                continue;

            lock (handlers)
            {
                foreach (var handler in handlers)
                    matchingHandlers.Add(handler);
            }
        }

        return Task.WhenAll(matchingHandlers.Select(handler => handler(context)));
    }

    internal void Register(string exchange, Func<ReceiveContext, Task> handler)
    {
        var list = _handlers.GetOrAdd(exchange, _ => new List<Func<ReceiveContext, Task>>());
        lock (list)
        {
            list.Add(handler);
        }
    }

    internal void Unregister(string exchange, Func<ReceiveContext, Task> handler)
    {
        if (_handlers.TryGetValue(exchange, out var list))
        {
            lock (list)
            {
                list.Remove(handler);
                if (list.Count == 0)
                    _handlers.TryRemove(exchange, out _);
            }
        }
    }

    private static string ExtractExchange(Uri address)
    {
        try
        {
            return address.Segments.LastOrDefault()?.Trim('/') ?? "default";
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Could not extract exchange from '{address}'", ex);
        }
    }

    class MediatorReceiveTransport : IReceiveTransport
    {
        private readonly MediatorTransportFactory _factory;
        private readonly string _exchange;
        private readonly Func<ReceiveContext, Task> _handler;
        private bool _started;

        public MediatorReceiveTransport(MediatorTransportFactory factory, string exchange, Func<ReceiveContext, Task> handler)
        {
            _factory = factory;
            _exchange = exchange;
            _handler = handler;
        }

        public Task Start(CancellationToken cancellationToken = default)
        {
            if (!_started)
            {
                _factory.Register(_exchange, _handler);
                _started = true;
            }
            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            if (_started)
            {
                _factory.Unregister(_exchange, _handler);
                _started = false;
            }
            return Task.CompletedTask;
        }
    }

    class MediatorSendTransport : ISendTransport
    {
        private readonly MediatorTransportFactory _factory;
        private readonly string _exchange;

        public MediatorSendTransport(MediatorTransportFactory factory, string exchange)
        {
            _factory = factory;
            _exchange = exchange;
        }

        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
        {
            var messageId = Guid.TryParse(context.MessageId, out var id) ? id : Guid.NewGuid();
            Guid? correlationId = context.CorrelationId != null && Guid.TryParse(context.CorrelationId, out var cId) ? cId : null;

            var messageTypes = context.MessageTypeUrns.ToList();

            var headers = new Dictionary<string, object>(context.Headers);

            var msgContext = new InMemoryMessageContext(
                message!,
                messageId,
                context.RequestId,
                correlationId,
                messageTypes,
                headers,
                context.ResponseAddress,
                context.FaultAddress,
                DateTimeOffset.UtcNow);

            var receiveContext = new ReceiveContextImpl(msgContext, null);
            var exchanges = MessageTypeCache.GetMessageTypes(typeof(T))
                .Select(EntityNameFormatter.Format)
                .Append(_exchange);
            return _factory.Dispatch(exchanges, receiveContext);
        }
    }

    class InMemoryMessageContext : IMessageContext
    {
        private readonly object _message;

        public InMemoryMessageContext(
            object message,
            Guid messageId,
            Guid? requestId,
            Guid? correlationId,
            IList<string> messageType,
            IDictionary<string, object> headers,
            Uri? responseAddress,
            Uri? faultAddress,
            DateTimeOffset sentTime)
        {
            _message = message;
            MessageId = messageId;
            RequestId = requestId;
            CorrelationId = correlationId;
            MessageType = messageType;
            Headers = headers;
            ResponseAddress = responseAddress;
            FaultAddress = faultAddress;
            SentTime = sentTime;
        }

        public Guid MessageId { get; }
        public Guid? RequestId { get; }
        public Guid? CorrelationId { get; }
        public IList<string> MessageType { get; }
        public Uri? ResponseAddress { get; }
        public Uri? FaultAddress { get; }
        public IDictionary<string, object> Headers { get; }
        public DateTimeOffset SentTime { get; }
        public string ContentType => InboundMessageResolver.EnvelopeContentType;
        public InboundMessageFormat Format => InboundMessageFormat.Envelope;

        public bool TryGetMessage<T>(out T? message) where T : class
        {
            if (_message is T msg)
            {
                message = msg;
                return true;
            }

            message = null;
            return false;
        }
    }
}
