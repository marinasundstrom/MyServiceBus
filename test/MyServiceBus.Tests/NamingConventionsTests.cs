using Xunit.Sdk;

namespace MyServiceBus.Tests;

public class SampleUrnMessage { }

public class NamingConventionsTests
{
    [Fact]
    [Throws(typeof(EqualException))]
    public void GetMessageUrn_ReturnsExpected()
    {
        var urn = NamingConventions.GetMessageUrn(typeof(SampleUrnMessage));
        Assert.Equal("urn:message:MyServiceBus.Tests:SampleUrnMessage", urn);
    }
}
