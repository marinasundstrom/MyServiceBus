namespace MyServiceBus;

/// <summary>
/// Identifies a one-way mediator request.
/// </summary>
public interface IRequest
{
}

/// <summary>
/// Identifies a mediator request and the response type it produces.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequest<out TResponse> : IRequest
    where TResponse : class
{
}
