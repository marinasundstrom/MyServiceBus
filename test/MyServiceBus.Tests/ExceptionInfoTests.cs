using System;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class ExceptionInfoTests
{
    [Fact]
    [Throws(typeof(EqualException))]
    public void FromException_captures_details()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ApplicationException("outer", inner);

        var info = ExceptionInfo.FromException(ex);

        Assert.Equal(typeof(ApplicationException).FullName, info.ExceptionType);
        Assert.Equal("outer", info.Message);
        Assert.Equal(typeof(InvalidOperationException).FullName, info.InnerException?.ExceptionType);
    }
}
