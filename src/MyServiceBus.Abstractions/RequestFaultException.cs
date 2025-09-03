using System;

namespace MyServiceBus;

public class RequestFaultException : Exception
{
    public RequestFaultException(string requestType, Fault fault)
            : base($"The {requestType} request faulted: {string.Join(Environment.NewLine, fault.Exceptions?.Select(x => x.Message) ?? [])}")
    {
    }
}
