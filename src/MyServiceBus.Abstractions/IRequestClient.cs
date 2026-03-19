namespace MyServiceBus;

public interface IRequestClient<TRequest>
    where TRequest : class
{
    /// <summary>
    /// Sends a request and waits for a single response message.
    /// </summary>
    /// <typeparam name="T">The expected response type.</typeparam>
    /// <param name="request">The request message.</param>
    /// <param name="contextCallback">An optional callback for configuring the send context.</param>
    /// <param name="cancellationToken">A cancellation token for the request operation.</param>
    /// <param name="timeout">An optional request timeout override.</param>
    /// <returns>The received response message.</returns>
    /// <exception cref="RequestFaultException">
    /// Thrown when the remote consumer responds with a fault for the request.
    /// </exception>
    Task<Response<T>> GetResponseAsync<T>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T : class;

    /// <summary>
    /// Sends a request and waits for one of two possible response message types.
    /// </summary>
    /// <typeparam name="T1">The first possible response type.</typeparam>
    /// <typeparam name="T2">The second possible response type.</typeparam>
    /// <param name="request">The request message.</param>
    /// <param name="contextCallback">An optional callback for configuring the send context.</param>
    /// <param name="cancellationToken">A cancellation token for the request operation.</param>
    /// <param name="timeout">An optional request timeout override.</param>
    /// <returns>The received response message.</returns>
    /// <exception cref="RequestFaultException">
    /// Thrown when the remote consumer responds with a fault for the request.
    /// </exception>
    Task<Response<T1, T2>> GetResponseAsync<T1, T2>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T1 : class
        where T2 : class;
}
