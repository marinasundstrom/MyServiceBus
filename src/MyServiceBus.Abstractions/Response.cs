using System.Diagnostics.CodeAnalysis;

namespace MyServiceBus;

#if NET11_0_OR_GREATER
[System.Runtime.CompilerServices.Union]
public class Response<T> : System.Runtime.CompilerServices.IUnion
#else
public class Response<T>
#endif
    where T : class
{
    public Response(T message)
    {
        Message = message;
    }

    public T Message { get; }

#if NET11_0_OR_GREATER
    public object Value => Message;

    public bool HasValue => true;

    public bool TryGetValue([NotNullWhen(true)] out T? message)
    {
        message = Message;
        return true;
    }
#endif
}

#if NET11_0_OR_GREATER
[System.Runtime.CompilerServices.Union]
public class Response<T1, T2> : System.Runtime.CompilerServices.IUnion
#else
public class Response<T1, T2>
#endif
    where T1 : class
    where T2 : class
{
    private readonly object _message;
#if NET11_0_OR_GREATER
    private readonly ResponseCase _case;

    public Response(T1 message)
    {
        _message = message;
        _case = ResponseCase.T1;
    }

    public Response(T2 message)
    {
        _message = message;
        _case = ResponseCase.T2;
    }

    public static Response<T1, T2> FromT1(T1 message) => new(message);

    public static Response<T1, T2> FromT2(T2 message) => new(message);
#else
    private Response(object message)
    {
        _message = message;
    }

    public static Response<T1, T2> FromT1(T1 message) => new(message);

    public static Response<T1, T2> FromT2(T2 message) => new(message);
#endif

#if NET11_0_OR_GREATER
    public object Value => _message;

    public bool HasValue => true;

    public bool TryGetValue([NotNullWhen(true)] out T1? message)
    {
        if (_case == ResponseCase.T1)
        {
            message = (T1)_message;
            return true;
        }

        message = null;
        return false;
    }

    public bool TryGetValue([NotNullWhen(true)] out T2? message)
    {
        if (_case == ResponseCase.T2)
        {
            message = (T2)_message;
            return true;
        }

        message = null;
        return false;
    }
#endif

    public bool Is<T>([NotNullWhen(true)] out Response<T>? response)
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

#if NET11_0_OR_GREATER
    private enum ResponseCase : byte
    {
        T1,
        T2
    }
#endif
}
