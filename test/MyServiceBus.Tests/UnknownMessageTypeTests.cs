using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

namespace MyServiceBus.Tests;

public class UnknownMessageTypeTests
{
    class ListLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = new();
        public List<string> Messages { get; } = new();
        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }

        class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose()
            {
            }
        }
    }

    class StubTransportFactory : ITransportFactory
    {
        [Throws(typeof(InvalidOperationException))]
        public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
            => Task.FromResult<ISendTransport>(new StubSendTransport());

        public Task<IReceiveTransport> CreateReceiveTransport(ReceiveEndpointTopology topology, Func<ReceiveContext, Task> handler, CancellationToken cancellationToken = default)
            => Task.FromResult<IReceiveTransport>(new StubReceiveTransport());

        class StubSendTransport : ISendTransport
        {
            public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default) where T : class
                => Task.CompletedTask;
        }

        class StubReceiveTransport : IReceiveTransport
        {
            public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Stop(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    class TestReceiveContext : ReceiveContext
    {
        readonly object message;

        public TestReceiveContext(object message, string messageType)
        {
            this.message = message;
            MessageType = new List<string> { messageType };
            Headers = new Dictionary<string, object>();
        }

        public Guid MessageId { get; } = Guid.NewGuid();
        public IList<string> MessageType { get; }
        public Uri? ResponseAddress => null;
        public Uri? FaultAddress => null;
        public Uri? ErrorAddress => null;
        public IDictionary<string, object> Headers { get; }
        public CancellationToken CancellationToken => CancellationToken.None;
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

    [Fact]
    [Throws(typeof(UriFormatException))]
    public async Task Logs_warning_for_unregistered_message_type()
    {
        var logger = new ListLogger<MessageBus>();
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<MessageBus>>(logger);
        var provider = services.BuildServiceProvider();

        var bus = new MessageBus(new StubTransportFactory(), provider, new SendPipe(Pipe.Empty<SendContext>()),
            new PublishPipe(Pipe.Empty<SendContext>()), new EnvelopeMessageSerializer(), new Uri("loopback://localhost/"));

        var context = new TestReceiveContext(new object(), "urn:message:Unknown");
        var method = typeof(MessageBus).GetMethod("HandleMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        await (Task)method!.Invoke(bus, new object[] { context })!;

        Assert.Contains(LogLevel.Warning, logger.Levels);
        Assert.Contains(logger.Messages, m => m.Contains("unregistered"));
    }
}
