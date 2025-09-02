using System;
using System.Text.RegularExpressions;

namespace MyServiceBus;

public interface IEndpointNameFormatter
{
    string Format(Type messageType);
}

public class DefaultEndpointNameFormatter : IEndpointNameFormatter
{
    public static readonly DefaultEndpointNameFormatter Instance = new();

    public string Format(Type messageType)
    {
        return messageType.Name;
    }
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

public class SnakeCaseEndpointNameFormatter : IEndpointNameFormatter
{
    public static readonly SnakeCaseEndpointNameFormatter Instance = new();

    [Throws(typeof(ArgumentException), typeof(RegexMatchTimeoutException))]
    public string Format(Type messageType)
    {
        var name = messageType.Name;
        return Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    }
}

