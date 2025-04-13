namespace MyServiceBus;

public interface IRequestClient<TRequest>
    where TRequest : class
{
    Task<Response<T>> GetResponseAsync<T>(TRequest request, CancellationToken cancellationToken = default, RequestTimeout timeout = default)
        where T : class;
}

public class Response<T>
    where T : class
{
    public Response(T message)
    {
        Message = message;
    }

    public T Message { get; set; } = default!;
}