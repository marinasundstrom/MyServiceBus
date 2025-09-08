using System;
using System.Reflection;
using MyServiceBus;
using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class SampleUrnMessage { }

[EntityName("custom-entity")]
public class AttributeMessage { }

class StaticFormatter : IMessageEntityNameFormatter
{
    public string FormatEntityName(Type messageType) => $"fmt-{messageType.Name.ToLowerInvariant()}";
}

public class NamingConventionsTests
{
    [Fact]
    [Throws(typeof(EqualException))]
    public void GetMessageUrn_ReturnsExpected()
    {
        var urn = NamingConventions.GetMessageUrn(typeof(SampleUrnMessage));
        Assert.Equal("urn:message:MyServiceBus.Tests:SampleUrnMessage", urn);
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(AmbiguousMatchException))]
    public void GetExchangeName_UsesAttribute()
    {
        var name = NamingConventions.GetExchangeName(typeof(AttributeMessage));
        Assert.Equal("custom-entity", name);
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(AmbiguousMatchException))]
    public void GetExchangeName_UsesFormatter()
    {
        var previous = NamingConventions.EntityNameFormatter;
        try
        {
            NamingConventions.SetEntityNameFormatter(new StaticFormatter());
            var name = NamingConventions.GetExchangeName(typeof(SampleUrnMessage));
            Assert.Equal("fmt-sampleurnmessage", name);
        }
        finally
        {
            NamingConventions.SetEntityNameFormatter(previous);
        }
    }
}
