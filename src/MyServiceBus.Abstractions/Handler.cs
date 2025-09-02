using System.Threading;

namespace MyServiceBus;

/// <summary>
/// Compatibility interface for Mediator handlers, providing a <c>Handle</c> method similar to MassTransit.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public interface IHandler<in TMessage> : IConsumer<TMessage>
    where TMessage : class
{
    /// <summary>
    /// Handle the incoming message.
    /// </summary>
    /// <param name="message">The message instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task Handle(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Default implementation of <see cref="IConsumer{TMessage}.Consume"/> that invokes <see cref="Handle"/>.
    /// </summary>
    Task IConsumer<TMessage>.Consume(ConsumeContext<TMessage> context)
        => Handle(context.Message, context.CancellationToken);
}

/// <summary>
/// Base class for mediator handlers.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public abstract class Handler<TMessage> : IHandler<TMessage>
    where TMessage : class
{
    /// <inheritdoc />
    public abstract Task Handle(TMessage message, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public Task Consume(ConsumeContext<TMessage> context)
        => Handle(context.Message, context.CancellationToken);
}

