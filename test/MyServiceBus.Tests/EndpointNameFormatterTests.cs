using System;
using MyServiceBus;
using Xunit.Sdk;
using Xunit;

namespace MyServiceBus.Tests;

public class EndpointNameFormatterTests
{
    class SampleMessage { }
    class SubmitOrderConsumer { }

    [Fact]
    public void Default_returns_type_name()
    {
        var name = DefaultEndpointNameFormatter.Instance.Format(typeof(SampleMessage));
        Assert.Equal(nameof(SampleMessage), name);
    }

    [Fact]
    public void Snake_case_formats_name()
    {
        var name = SnakeCaseEndpointNameFormatter.Instance.Format(typeof(SampleMessage));
        Assert.Equal("sample_message", name);
    }

    [Fact]
    public void Formatters_trim_MassTransit_endpoint_suffixes()
    {
        Assert.Equal("SubmitOrder", DefaultEndpointNameFormatter.Instance.Format(typeof(SubmitOrderConsumer)));
        Assert.Equal("submit-order", KebabCaseEndpointNameFormatter.Instance.Format(typeof(SubmitOrderConsumer)));
        Assert.Equal("submit_order", SnakeCaseEndpointNameFormatter.Instance.Format(typeof(SubmitOrderConsumer)));
    }
}
