using System;
using System.Text.RegularExpressions;

namespace MyServiceBus;

public interface IEndpointNameFormatter
{
    string Format(Type messageType);
}

public class KebabCaseEndpointNameFormatter : IEndpointNameFormatter
{
    public static readonly KebabCaseEndpointNameFormatter Instance = new();

    [Throws(typeof(ArgumentException), typeof(RegexMatchTimeoutException))]
    public string Format(Type messageType)
    {
        var name = messageType.Name;
        return Regex.Replace(name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
    }
}

