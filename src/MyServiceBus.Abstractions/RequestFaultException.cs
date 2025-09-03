using System;
using System.Linq;

namespace MyServiceBus;

public class RequestFaultException : Exception
{
    public RequestFaultException(string requestType, Fault fault)
        : base($"The {requestType} request faulted: {string.Join(Environment.NewLine, fault.Exceptions.Select(x => x.Message))}")
    {
        RequestType = requestType;
        Fault = fault;
    }

    public string RequestType { get; }

    public Fault Fault { get; }
}
