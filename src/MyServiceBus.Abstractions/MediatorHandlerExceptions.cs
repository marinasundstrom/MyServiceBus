namespace MyServiceBus;

/// <summary>
/// Thrown when mediator <c>Send</c> cannot find a compatible handler.
/// </summary>
public sealed class MediatorHandlerNotFoundException : InvalidOperationException
{
    public MediatorHandlerNotFoundException(Type messageType)
        : base($"No mediator handler is registered for message type '{messageType.FullName}'.")
    {
        MessageType = messageType;
    }

    public Type MessageType { get; }
}

/// <summary>
/// Thrown when mediator <c>Send</c> finds more than one compatible handler.
/// </summary>
public sealed class MediatorHandlerCardinalityException : InvalidOperationException
{
    public MediatorHandlerCardinalityException(Type messageType, IReadOnlyList<Type> handlerTypes)
        : base($"Mediator Send requires exactly one handler for '{messageType.FullName}', but found {handlerTypes.Count}: {string.Join(", ", handlerTypes.Select(type => type.FullName))}.")
    {
        MessageType = messageType;
        HandlerTypes = handlerTypes;
    }

    public Type MessageType { get; }

    public IReadOnlyList<Type> HandlerTypes { get; }
}

/// <summary>
/// Thrown when the registered result handler does not produce the response type requested by mediator <c>Send</c>.
/// </summary>
public sealed class MediatorResponseTypeException : InvalidOperationException
{
    public MediatorResponseTypeException(Type messageType, Type responseType, Type handlerType)
        : base(responseType == typeof(void)
            ? $"Mediator handler '{handlerType.FullName}' produces a response for '{messageType.FullName}'. Use Send<TRequest, TResponse> instead."
            : $"Mediator handler '{handlerType.FullName}' cannot produce response type '{responseType.FullName}' for '{messageType.FullName}'.")
    {
        MessageType = messageType;
        ResponseType = responseType;
        HandlerType = handlerType;
    }

    public Type MessageType { get; }

    public Type ResponseType { get; }

    public Type HandlerType { get; }
}
