namespace MyServiceBus;

/// <summary>
/// Dispatches commands, queries, and notifications inside the current process.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Publishes a notification to every compatible local handler.
    /// </summary>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : class;

    /// <summary>
    /// Sends a command to exactly one compatible local handler.
    /// </summary>
    /// <exception cref="MediatorHandlerNotFoundException">No compatible handler is registered.</exception>
    /// <exception cref="MediatorHandlerCardinalityException">More than one compatible handler is registered.</exception>
    /// <exception cref="MediatorResponseTypeException">The selected handler produces a response.</exception>
    Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class;

    /// <summary>
    /// Sends a command or query to exactly one compatible local handler and returns its response.
    /// </summary>
    /// <exception cref="MediatorHandlerNotFoundException">No compatible handler is registered.</exception>
    /// <exception cref="MediatorHandlerCardinalityException">More than one compatible handler is registered.</exception>
    /// <exception cref="MediatorResponseTypeException">The selected handler cannot produce <typeparamref name="TResponse"/>.</exception>
    Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Creates a request client for MassTransit-familiar request/response usage.
    /// </summary>
    /// <exception cref="MediatorHandlerNotFoundException">No compatible handler is registered.</exception>
    /// <exception cref="MediatorHandlerCardinalityException">More than one compatible handler is registered.</exception>
    IRequestClient<TRequest> CreateRequestClient<TRequest>()
        where TRequest : class;
}
