using System;

namespace MyServiceBus;

public class RequestFaultException : Exception
{
    public RequestFaultException(string message)
        : base(message)
    {
    }
}
