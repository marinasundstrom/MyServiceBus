using System;
using Xunit;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class ThrowsAttributeTests
{
    [Fact]
    [Throws(typeof(EqualException))]
    public void Stores_exception_types()
    {
        var attr = new ThrowsAttribute(typeof(InvalidOperationException), typeof(ArgumentException));
        Assert.Equal(new[] { typeof(InvalidOperationException), typeof(ArgumentException) }, attr.ExceptionTypes);
    }

    [Fact]
    public void Throws_when_non_exception_type()
    {
        Assert.Throws<ArgumentException>(() => new ThrowsAttribute(typeof(string)));
    }
}
