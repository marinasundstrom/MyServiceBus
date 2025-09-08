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

public class FormatterTests
{
    [Fact]
    [Throws(typeof(EqualException))]
    public void GetMessageUrn_ReturnsExpected()
    {
        var urn = MessageUrn.For(typeof(SampleUrnMessage));
        Assert.Equal("urn:message:MyServiceBus.Tests:SampleUrnMessage", urn);
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public void GetExchangeName_UsesAttribute()
    {
        var name = EntityNameFormatter.Format(typeof(AttributeMessage));
        Assert.Equal("custom-entity", name);
    }

    [Fact]
    [Throws(typeof(EqualException), typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    public void GetExchangeName_UsesFormatter()
    {
        var previous = EntityNameFormatter.Formatter;
        try
        {
            EntityNameFormatter.SetFormatter(new StaticFormatter());
            var name = EntityNameFormatter.Format(typeof(SampleUrnMessage));
            Assert.Equal("fmt-sampleurnmessage", name);
        }
        finally
        {
            EntityNameFormatter.SetFormatter(previous);
        }
    }
}
