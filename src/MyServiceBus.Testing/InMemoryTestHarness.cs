using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class InMemoryTestHarness
{
    readonly Dictionary<Type, List<Func<ConsumeContext, Task>>> handlers = new();
    readonly List<object> consumed = new();

    public IReadOnlyCollection<object> Consumed => consumed.AsReadOnly();

    public Task Start() => Task.CompletedTask;

    public Task Stop() => Task.CompletedTask;

    public void RegisterHandler<T>(Func<ConsumeContext<T>, Task> handler) where T : class
    {
        if (!handlers.TryGetValue(typeof(T), out var list))
        {
            list = new List<Func<ConsumeContext, Task>>();
            handlers.Add(typeof(T), list);
        }

        list.Add(ctx => handler((ConsumeContext<T>)ctx));
    }

    public async Task Send<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        if (handlers.TryGetValue(message!.GetType(), out var list))
        {
            foreach (var handler in list)
            {
                var context = new TestConsumeContext<T>(this, message, cancellationToken);
                await handler(context).ConfigureAwait(false);
                consumed.Add(message);
            }
        }
    }

    public bool WasConsumed<T>() where T : class => consumed.OfType<T>().Any();

    internal Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
        => Send(message, cancellationToken);

    class TestConsumeContext<T> : ConsumeContext<T> where T : class
    {
        readonly InMemoryTestHarness harness;

        public TestConsumeContext(InMemoryTestHarness harness, T message, CancellationToken cancellationToken)
        {
            this.harness = harness;
            Message = message;
            CancellationToken = cancellationToken;
        }

        public T Message { get; }

        public CancellationToken CancellationToken { get; }

        public ISendEndpoint GetSendEndpoint(Uri uri) => new HarnessSendEndpoint(harness);

        public Task PublishAsync<TMessage>(object message, CancellationToken cancellationToken = default) where TMessage : class
            => harness.Send((TMessage)message, cancellationToken);

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class
            => harness.Send(message, cancellationToken);

        public Task RespondAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class
            => harness.Send(message, cancellationToken);
    }

    class HarnessSendEndpoint : ISendEndpoint
    {
        readonly InMemoryTestHarness harness;

        public HarnessSendEndpoint(InMemoryTestHarness harness) => this.harness = harness;

        public Task Send<T>(object message, CancellationToken cancellationToken = default) where T : class
            => harness.Send((T)message, cancellationToken);

        public Task Send<T>(T message, CancellationToken cancellationToken = default) where T : class
            => harness.Send(message, cancellationToken);
    }
}
