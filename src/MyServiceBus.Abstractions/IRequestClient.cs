namespace MyServiceBus;

public interface IRequestClient<TRequest>
{
    Task<TResponse> GetResponse<TResponse>(TRequest request, CancellationToken cancellationToken = default);
}
