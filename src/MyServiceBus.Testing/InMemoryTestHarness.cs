using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public class InMemoryTestHarness : IMessageBus, ITransportFactory, IReceiveEndpointConnector
{
    readonly Dictionary<Type, List<Func<ReceiveContext, Task>>> handlers = new();
    readonly List<Func<ReceiveContext, Task>> receiveHandlers = new();
    readonly List<object> consumed = new();
    readonly HashSet<Type> consumerTypes = new();
    readonly IServiceProvider? provider;
    readonly IBusTopology topology;
    readonly ISendContextFactory _sendContextFactory;
    readonly IPublishContextFactory _publishContextFactory;

    [Throws(typeof(UriFormatException))]
    public Uri Address => new("loopback://localhost/");
    public IBusTopology Topology => topology;

    public IReadOnlyCollection<object> Consumed => consumed.AsReadOnly();

    public InMemoryTestHarness()
    {
        topology = new TopologyRegistry();
        _sendContextFactory = new SendContextFactory();
        _publishContextFactory = new PublishContextFactory();
    }

    public InMemoryTestHarness(IServiceProvider provider)
    {
        this.provider = provider;
        topology = provider.GetService<TopologyRegistry>() ?? new TopologyRegistry();
        _sendContextFactory = provider.GetService<ISendContextFactory>() ?? new SendContextFactory();
        _publishContextFactory = provider.GetService<IPublishContextFactory>() ?? new PublishContextFactory();
    }

    public Task Start() => StartAsync(CancellationToken.None);

    public Task Stop() => StopAsync(CancellationToken.None);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (provider != null)
        {
            foreach (var action in provider.GetServices<IPostBuildAction>())
            {
                action.Execute(provider);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void RegisterHandler<T>(Func<ConsumeContext<T>, Task> handler) where T : class
    {
        if (!handlers.TryGetValue(typeof(T), out var list))
        {
            list = new List<Func<ReceiveContext, Task>>();
            handlers.Add(typeof(T), list);
        }

        list.Add(async ctx =>
        {
            if (ctx.TryGetMessage<T>(out var msg))
            {
                var consumeContext = new TestConsumeContext<T>(this, msg!, ctx);
                await handler(consumeContext).ConfigureAwait(false);
                consumed.Add(msg!);
            }
        });
    }

    public bool WasConsumed<T>() where T : class => consumed.OfType<T>().Any();

    public async Task<bool> WaitForConsumed<T>(TimeSpan? timeout = null) where T : class
    {
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(1));
        while (DateTime.UtcNow < end)
        {
            if (WasConsumed<T>())
                return true;
            await Task.Delay(10).ConfigureAwait(false);
        }

        return WasConsumed<T>();
    }

    [Throws(typeof(InvalidCastException))]
    public Task Publish<TMessage>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
        => Publish((TMessage)message, contextCallback, cancellationToken);

    public Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        var serializer = provider?.GetService<IMessageSerializer>() ?? new EnvelopeMessageSerializer();
        var publishContext = _publishContextFactory.Create(MessageTypeCache.GetMessageTypes(typeof(T)), serializer, cancellationToken);
        publishContext.MessageId = Guid.NewGuid().ToString();

        contextCallback?.Invoke(publishContext);

        return InternalSend(message, publishContext);
    }

    public IPublishEndpoint GetPublishEndpoint() => this;

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri) => Task.FromResult<ISendEndpoint>(new HarnessSendEndpoint(this));

    [Throws(typeof(InvalidOperationException))]
    public Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        if (provider == null)
            throw new InvalidOperationException("Service provider is required to add consumers");

        if (!consumerTypes.Add(typeof(TConsumer)))
            return Task.CompletedTask;

        var factory = provider.GetRequiredService<IConsumerFactory<TConsumer>>();
        RegisterHandler<TMessage>(context =>
            factory.Send(context,
                Pipe.Execute<ConsumerConsumeContext<TConsumer, TMessage>>(x => x.Consumer.Consume(x))));

        return Task.CompletedTask;
    }

    public Task AddHandler<TMessage>(string queueName, string exchangeName, Func<ConsumeContext<TMessage>, Task> handler,
        int? retryCount = null, TimeSpan? retryDelay = null, ushort? prefetchCount = null,
        IDictionary<string, object?>? queueArguments = null, IMessageSerializer? serializer = null, CancellationToken cancellationToken = default) where TMessage : class
    {
        RegisterHandler(handler);
        return Task.CompletedTask;
    }

    public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
        => Task.FromResult<ISendTransport>(new HarnessSendTransport(this));

    public Task<IReceiveTransport> CreateReceiveTransport(
        EndpointDefinition definition,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReceiveTransport>(new HarnessReceiveTransport(this, handler));
    }

    internal async Task InternalSend<T>(T message, SendContext context) where T : class
    {
        var messageId = Guid.TryParse(context.MessageId, out var id) ? id : Guid.NewGuid();
        Guid? correlationId = context.CorrelationId != null && Guid.TryParse(context.CorrelationId, out var cId) ? cId : null;

        var messageTypes = MessageTypeCache
            .GetMessageTypes(message!.GetType())
            .Select(t => MessageUrn.For(t))
            .ToList();

        var headers = new Dictionary<string, object>(context.Headers);

        var msgContext = new InMemoryMessageContext(
            message!,
            messageId,
            correlationId,
            messageTypes,
            headers,
            context.ResponseAddress,
            context.FaultAddress,
            DateTimeOffset.UtcNow);

        var receiveContext = new ReceiveContextImpl(msgContext, null, context.CancellationToken);

        List<Func<ReceiveContext, Task>> snapshot;
        lock (receiveHandlers)
            snapshot = receiveHandlers.ToList();

        foreach (var handler in snapshot)
            await handler(receiveContext).ConfigureAwait(false);

        if (handlers.TryGetValue(message!.GetType(), out var list))
        {
            foreach (var h in list)
                await h(receiveContext).ConfigureAwait(false);
        }
    }

    class TestConsumeContext<T> : ConsumeContext<T> where T : class
    {
        readonly InMemoryTestHarness harness;
        readonly ReceiveContext receiveContext;

        public TestConsumeContext(InMemoryTestHarness harness, T message, ReceiveContext receiveContext)
        {
            this.harness = harness;
            this.receiveContext = receiveContext;
            Message = message;
        }

        public T Message { get; }

        public CancellationToken CancellationToken => receiveContext.CancellationToken;

        public Task<ISendEndpoint> GetSendEndpoint(Uri uri) => Task.FromResult<ISendEndpoint>(new HarnessSendEndpoint(harness));
        [Throws(typeof(InvalidCastException))]
        public Task Publish<TMessage>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
            => harness.Publish((TMessage)message, contextCallback, cancellationToken);

        public Task Publish<TMessage>(TMessage message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
            => harness.Publish(message, contextCallback, cancellationToken);

        public Task RespondAsync<TMessage>(TMessage message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
            => harness.Publish((dynamic)message!, contextCallback != null ? new Action<IPublishContext>(ctx => contextCallback(ctx)) : null, cancellationToken);

        public Task RespondAsync<TMessage>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
            => harness.Publish((dynamic)message!, contextCallback != null ? new Action<IPublishContext>(ctx => contextCallback(ctx)) : null, cancellationToken);

        public Task Send<TMessage>(Uri address, TMessage message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
            => Send<TMessage>(address, (object)message!, contextCallback, cancellationToken);

        public async Task Send<TMessage>(Uri address, object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where TMessage : class
        {
            var endpoint = await GetSendEndpoint(address).ConfigureAwait(false);
            await endpoint.Send<TMessage>(message, contextCallback, cancellationToken).ConfigureAwait(false);
        }

        [Throws(typeof(InvalidCastException))]
        public Task Forward<T>(Uri address, T message, CancellationToken cancellationToken = default) where T : class
            => Forward<T>(address, (object)message!, cancellationToken);

        public async Task Forward<T>(Uri address, object message, CancellationToken cancellationToken = default) where T : class
        {
            var endpoint = await GetSendEndpoint(address).ConfigureAwait(false);
            await endpoint.Send<T>(message, null, cancellationToken).ConfigureAwait(false);
        }
    }

    class HarnessSendEndpoint : ISendEndpoint
    {
        readonly InMemoryTestHarness harness;

        public HarnessSendEndpoint(InMemoryTestHarness harness) => this.harness = harness;

        public Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
            => harness.Publish((dynamic)message!, contextCallback != null ? new Action<IPublishContext>(ctx => contextCallback(ctx)) : null, cancellationToken);
        public Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
            => harness.Publish((dynamic)message!, contextCallback != null ? new Action<IPublishContext>(ctx => contextCallback(ctx)) : null, cancellationToken);
    }

    class HarnessReceiveTransport : IReceiveTransport
    {
        readonly InMemoryTestHarness harness;
        readonly Func<ReceiveContext, Task> handler;

        public HarnessReceiveTransport(InMemoryTestHarness harness, Func<ReceiveContext, Task> handler)
        {
            this.harness = harness;
            this.handler = handler;
        }

        public Task Start(CancellationToken cancellationToken = default)
        {
            lock (harness.receiveHandlers)
                harness.receiveHandlers.Add(handler);
            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            lock (harness.receiveHandlers)
                harness.receiveHandlers.Remove(handler);
            return Task.CompletedTask;
        }
    }

    class HarnessSendTransport : ISendTransport
    {
        readonly InMemoryTestHarness harness;

        public HarnessSendTransport(InMemoryTestHarness harness) => this.harness = harness;

        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
            => harness.InternalSend(message, context);
    }

    class InMemoryMessageContext : IMessageContext
    {
        readonly object message;

        public InMemoryMessageContext(
            object message,
            Guid messageId,
            Guid? correlationId,
            IList<string> messageType,
            IDictionary<string, object> headers,
            Uri? responseAddress,
            Uri? faultAddress,
            DateTimeOffset sentTime)
        {
            this.message = message;
            MessageId = messageId;
            CorrelationId = correlationId;
            MessageType = messageType;
            Headers = headers;
            ResponseAddress = responseAddress;
            FaultAddress = faultAddress;
            SentTime = sentTime;
        }

        public Guid MessageId { get; }
        public Guid? CorrelationId { get; }
        public IList<string> MessageType { get; }
        public Uri? ResponseAddress { get; }
        public Uri? FaultAddress { get; }
        public IDictionary<string, object> Headers { get; }
        public DateTimeOffset SentTime { get; }

        [Throws(typeof(ObjectDisposedException))]
        public bool TryGetMessage<T>(out T? msg) where T : class
        {
            if (message is T m)
            {
                msg = m;
                return true;
            }

            msg = null;
            return false;
        }
    }
}
