using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public interface IMessageBus :
    IPublishEndpoint,
    IPublishEndpointProvider,
    ISendEndpointProvider
{
    /// <summary>
    /// The InputAddress of the default bus endpoint
    /// </summary>
    Uri Address { get; }

    /// <summary>
    /// The bus topology
    /// </summary>
    IBusTopology Topology { get; }

    /// <exception cref="UnsupportedTransportCapabilityException">
    /// Thrown before receive transports start when the selected transport cannot satisfy an explicitly required capability.
    /// </exception>
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops receiving and allows active work to drain within the supplied timeout.
    /// </summary>
    /// <exception cref="BusStopTimeoutException">
    /// The timeout elapsed before every receive transport completed its drain.
    /// </exception>
    async Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), "The stop timeout must be positive or infinite.");

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await StopAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await StopAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw new BusStopTimeoutException(timeout, exception);
        }
    }

    Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    Task AddHandler<TMessage>(string queueName, string exchangeName, Func<ConsumeContext<TMessage>, Task> handler,
        int? retryCount = null, TimeSpan? retryDelay = null, ushort? prefetchCount = null,
        IDictionary<string, object?>? queueArguments = null, IMessageSerializer? serializer = null,
        CancellationToken cancellationToken = default, int? concurrentMessageLimit = null)
        where TMessage : class;
}
