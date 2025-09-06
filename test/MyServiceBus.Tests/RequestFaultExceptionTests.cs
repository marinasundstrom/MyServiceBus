using System;
using MyServiceBus;
using Xunit;

public class RequestFaultExceptionTests
{
    class DummyRequest
    {
    }

    [Fact]
    [Throws(typeof(ContainsException))]
    public void Uses_exception_type_when_message_missing()
    {
        var fault = new Fault<DummyRequest>
        {
            Message = new DummyRequest(),
            Exceptions = [
                new ExceptionInfo { ExceptionType = "System.InvalidOperationException", Message = null! }
            ]
        };

        var ex = new RequestFaultException(typeof(DummyRequest).Name, fault);
        Assert.Contains("System.InvalidOperationException", ex.Message);
    }
}
