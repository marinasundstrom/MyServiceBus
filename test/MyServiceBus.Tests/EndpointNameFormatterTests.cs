using System;
using MyServiceBus;
using Xunit.Sdk;
using Xunit;

namespace MyServiceBus.Tests;

public class EndpointNameFormatterTests
{
    class SampleMessage { }

    [Fact]
    [Throws(typeof(EqualException))]
    public void Default_returns_type_name()
    {
        var name = DefaultEndpointNameFormatter.Instance.Format(typeof(SampleMessage));
        Assert.Equal(nameof(SampleMessage), name);
    }

    [Fact]
    [Throws(typeof(System.Text.RegularExpressions.RegexMatchTimeoutException), typeof(EqualException))]
    public void Snake_case_formats_name()
    {
        var name = SnakeCaseEndpointNameFormatter.Instance.Format(typeof(SampleMessage));
        Assert.Equal("sample_message", name);
    }
}
