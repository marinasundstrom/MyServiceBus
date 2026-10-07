using System.Diagnostics.CodeAnalysis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IPublishEndpoint
{
    /// <summary>Publishes using the concrete runtime contract.</summary>
    [RequiresDynamicCode("Runtime publication closes generic contracts dynamically. Use Publish<T> with known contracts for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime publication needs preserved generic contract metadata.")]
    Task Publish(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
        => RuntimePublish.Publish(this, message, message?.GetType() ?? throw new ArgumentNullException(nameof(message)), contextCallback, cancellationToken);

    /// <summary>Publishes using an explicitly selected runtime contract.</summary>
    /// <exception cref="ArgumentException">The message is not assignable to a closed reference contract.</exception>
    [RequiresDynamicCode("Runtime publication closes generic contracts dynamically. Use Publish<T> with known contracts for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime publication needs preserved generic contract metadata.")]
    Task Publish(object message, Type messageType, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default)
        => RuntimePublish.Publish(this, message, messageType, contextCallback, cancellationToken);

    Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class;

    Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class;
}
