namespace MyServiceBus;

public interface IRequestClient<TRequest>
    where TRequest : class
{
    [Throws(typeof(RequestFaultException))]
    Task<Response<T>> GetResponseAsync<T>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T : class;

    [Throws(typeof(RequestFaultException))]
    Task<Response<T1, T2>> GetResponseAsync<T1, T2>(TRequest request, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T1 : class
        where T2 : class;
}