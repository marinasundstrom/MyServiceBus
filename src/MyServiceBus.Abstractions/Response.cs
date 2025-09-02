namespace MyServiceBus;

public class Response<T>
    where T : class
{
    public Response(T message)
    {
        Message = message;
    }

    public T Message { get; }
}

public class Response<T1, T2>
    where T1 : class
    where T2 : class
{
    private readonly object _message;

    private Response(object message)
    {
        _message = message;
    }

    public static Response<T1, T2> FromT1(T1 message) => new Response<T1, T2>(message);

    public static Response<T1, T2> FromT2(T2 message) => new Response<T1, T2>(message);

    public bool Is<T>(out Response<T> response)
        where T : class
    {
        if (_message is T typed)
        {
            response = new Response<T>(typed);
            return true;
        }

        response = null!;
        return false;
    }
}
