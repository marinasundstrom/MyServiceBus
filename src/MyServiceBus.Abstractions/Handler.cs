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

/// <summary>
/// Compatibility interface for Mediator handlers that produce a response.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResult">The response type.</typeparam>
public interface IHandler<in TMessage, TResult> : IConsumer<TMessage>
    where TMessage : class
    where TResult : class
{
    /// <summary>
    /// Handle the incoming message and produce a response.
    /// </summary>
    /// <param name="message">The message instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<TResult> Handle(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Default implementation of <see cref="IConsumer{TMessage}.Consume"/> that invokes <see cref="Handle"/>
    /// and responds with the result.
    /// </summary>
    async Task IConsumer<TMessage>.Consume(ConsumeContext<TMessage> context)
    {
        var result = await Handle(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(result, null, context.CancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Base class for mediator handlers that produce a response.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResult">The response type.</typeparam>
public abstract class Handler<TMessage, TResult> : IHandler<TMessage, TResult>
    where TMessage : class
    where TResult : class
{
    /// <inheritdoc />
    public abstract Task<TResult> Handle(TMessage message, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        var result = await Handle(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(result, null, context.CancellationToken).ConfigureAwait(false);
    }
}


