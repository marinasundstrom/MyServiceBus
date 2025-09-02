namespace MyServiceBus;

public interface IRequestClient<TRequest>
    where TRequest : class
{
    Task<Response<T>> GetResponseAsync<T>(TRequest request, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T : class;

    Task<Response<T1, T2>> GetResponseAsync<T1, T2>(TRequest request, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T1 : class
        where T2 : class;
}